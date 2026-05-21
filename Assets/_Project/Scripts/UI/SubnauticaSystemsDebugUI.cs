using System;
using System.Collections.Generic;
using Hecton8;
using Hecton8.AI;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using Hecton8.Environment;
using Hecton8.VFX;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Temporary runtime overlay that surfaces the core Subnautica-gap systems during play mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SubnauticaSystemsDebugUI : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        // COLD ALLOC: List<SuitHUDV4CanvasOverlay>(4) â€” overlay canvas resolution buffer â€” owner: SubnauticaSystemsDebugUI
        private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer = new List<SuitHUDV4CanvasOverlay>(4);
        private static SubnauticaSystemsDebugUI s_activeRuntimeInstance;
        private static bool s_isBootstrappingRuntimeOverlay;
        [ThreadStatic] private static char[] s_runtimeSnapshotNumberBuffer;

        internal static SubnauticaSystemsDebugUI ActiveRuntimeInstance => s_activeRuntimeInstance;

        [SerializeField] private bool logLifecycleDiagnostics;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntimeInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeOverlayInstances()
        {
            if (!Application.isPlaying)
                return;

            if (!ShouldAutoCreateRuntimeOverlay(SceneManager.GetActiveScene()))
                return;

            // COLD ALLOC: SubnauticaSystemsDebugUI[] â€” runtime overlay recovery after scene load â€” owner: SubnauticaSystemsDebugUI
            if (s_activeRuntimeInstance != null)
            {
                if (!s_activeRuntimeInstance.enabled)
                    s_activeRuntimeInstance.enabled = true;

                s_activeRuntimeInstance.QueueRuntimeBootstrap(forceManagerResolve: true);

                return;
            }

            // COLD ALLOC: GameObject[1] â€” runtime debug overlay fallback when the scene instance is missing â€” owner: SubnauticaSystemsDebugUI
            GameObject runtimeRoot = new GameObject("SubnauticaSystemsDebugUI_Auto");
            SubnauticaSystemsDebugUI runtimeOverlay = runtimeRoot.AddComponent<SubnauticaSystemsDebugUI>();
            runtimeOverlay.QueueRuntimeBootstrap(forceManagerResolve: true);
        }

        private static bool ShouldAutoCreateRuntimeOverlay(Scene scene)
        {
            return scene.IsValid() &&
                string.Equals(scene.name, "02_HECTON_WORLD", StringComparison.Ordinal);
        }

        private const string RootObjectName = "SubnauticaSystemsDebugUI_Panel";
        private const string CanvasObjectName = "SubnauticaSystemsDebugUI_Canvas";
        private const string MissingLabel = "MISSING";
        private const string DisabledLabel = "OFF";
        private const string EnabledLabel = "ON";
        private const string ReadyLabel = "READY";
        private const string PendingLabel = "PENDING";

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Tooltip("Optional explicit HUD canvas. If null, the overlay resolves the active suit HUD canvas at runtime.")]
        private Canvas targetCanvas;

        [SerializeField, Tooltip("Optional TMP font asset. If null, TMP default font is used.")]
        private TMP_FontAsset fontAsset;

        [Header("â”€â”€ Layout â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Tooltip("Top-left anchored position for the debug panel.")]
        private Vector2 anchoredPosition = new Vector2(26f, -28f);

        [SerializeField, Tooltip("Panel size in canvas pixels.")]
        private Vector2 panelSize = new Vector2(448f, 388f);

        [SerializeField, Tooltip("Refresh cadence for the overlay text.")]
        [Range(0.1f, 2f)]
        private float refreshInterval = 0.2f;

        [SerializeField, Tooltip("Cold-path retry cadence for manager resolution when the world is still bootstrapping.")]
        [Range(0.1f, 2f)]
        private float managerResolveRetryInterval = 0.5f;

        [SerializeField, Tooltip("Keeps the temporary debug owner alive through bootstrap scene transitions.")]
        private bool persistAcrossSceneLoads = false;

        [Header("â”€â”€ Stress Harness â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Tooltip("Development-only override that forces DynamicResolutionScaler into a pressured state.")]
        private bool enableStressTest = false;

        [SerializeField, Tooltip("Forced frame time used by the development stress harness.")]
        [Range(16.67f, 80f)]
        private float forcedFrameTimeMs = 33f;

        [SerializeField, Tooltip("Direct render-scale clamp used by the stress harness so adaptive consumers can be proofed at 0.50 without changing quality presets.")]
        [Range(0.1f, 1f)]
        private float forcedRenderScale = 0.5f;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private bool debugCanvasResolved;
        [SerializeField] private string debugSceneName = "None";
        [SerializeField] private string debugBootstrapState = "PENDING";
        [SerializeField] private string debugTickCounts = "MISSING";
        [SerializeField] private string debugRenderPressure = "Stable";
        [SerializeField] private string debugFaunaBiome = "None";
        [SerializeField] private string debugFaunaBias = "None";
        [SerializeField] private string debugMusicProfile = "None";
        [SerializeField] private string debugSoundscapeTier = "Surface";
        [SerializeField] private string debugUnderwaterBudget = "MISSING";
        [SerializeField] private string debugCameraBudget = "MISSING";

        private RectTransform _root;
        private Canvas _runtimeCanvas;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _titleValue;
        private TextMeshProUGUI _sceneValue;
        private TextMeshProUGUI _bootstrapValue;
        private TextMeshProUGUI _tickCountsValue;
        private TextMeshProUGUI _renderScaleValue;
        private TextMeshProUGUI _renderPressureValue;
        private TextMeshProUGUI _faunaBiomeValue;
        private TextMeshProUGUI _faunaBiasValue;
        private TextMeshProUGUI _faunaLimitValue;
        private TextMeshProUGUI _musicTensionValue;
        private TextMeshProUGUI _musicProfileValue;
        private TextMeshProUGUI _soundscapeTierValue;
        private TextMeshProUGUI _underwaterBudgetValue;
        private TextMeshProUGUI _cameraBudgetValue;
        private TextMeshProUGUI _stressValue;
        private bool _registered;
        private bool _slowTickRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _diagnosticsRefreshPending;
        private bool _stressApplied;
        private float _refreshTimer;
        private float _nextManagerResolveAttemptTime = float.NegativeInfinity;
        private DynamicResolutionScaler _resolvedScaler;
        private FaunaDirector _resolvedFaunaDirector;
        private HectonMusicDirector _resolvedMusicDirector;
        private SoundscapeSystem _resolvedSoundscapeSystem;
        private HectonUnderwaterVisuals _resolvedUnderwaterVisuals;
        private ICameraJuiceSystem _resolvedCameraJuiceSystem;
        private string _lastTitleValue = string.Empty;
        private string _lastSceneValue = string.Empty;
        private string _lastBootstrapValue = string.Empty;
        private string _lastTickCountsValue = string.Empty;
        private string _lastRenderScaleValue = string.Empty;
        private string _lastRenderPressureValue = string.Empty;
        private string _lastFaunaBiomeValue = string.Empty;
        private string _lastFaunaBiasValue = string.Empty;
        private string _lastFaunaLimitValue = string.Empty;
        private string _lastMusicTensionValue = string.Empty;
        private string _lastMusicProfileValue = string.Empty;
        private string _lastSoundscapeTierValue = string.Empty;
        private string _lastUnderwaterBudgetValue = string.Empty;
        private string _lastCameraBudgetValue = string.Empty;
        private string _lastStressValue = string.Empty;
        private bool _runtimeSnapshotLogged;
        private string _runtimeSnapshotScene = string.Empty;
        private bool _bootstrapPending = true;
        private bool _forceManagerResolveOnBootstrap = true;
        private float _nextBootstrapAttemptTime = float.NegativeInfinity;
        // COLD ALLOC: char[32] - tick-count diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _tickCountsBuffer = new char[32];
        // COLD ALLOC: char[16] - render-scale diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _renderScaleBuffer = new char[16];
        // COLD ALLOC: char[40] - fauna-limit diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _faunaLimitsBuffer = new char[40];
        // COLD ALLOC: char[16] - music-tension diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _musicTensionBuffer = new char[16];
        // COLD ALLOC: char[40] - underwater budget diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _underwaterBudgetBuffer = new char[40];
        // COLD ALLOC: char[40] - camera budget diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _cameraBudgetBuffer = new char[40];
        // COLD ALLOC: char[32] - stress harness diagnostic buffer - owner: SubnauticaSystemsDebugUI
        private readonly char[] _stressBuffer = new char[32];
        private int _lastTickCountsHash;
        private int _lastTickCountsLength = -1;
        private int _lastRenderScaleHash;
        private int _lastRenderScaleLength = -1;
        private int _lastFaunaLimitHash;
        private int _lastFaunaLimitLength = -1;
        private int _lastMusicTensionHash;
        private int _lastMusicTensionLength = -1;
        private int _lastUnderwaterBudgetHash;
        private int _lastUnderwaterBudgetLength = -1;
        private int _lastCameraBudgetHash;
        private int _lastCameraBudgetLength = -1;
        private int _lastStressHash;
        private int _lastStressLength = -1;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (s_activeRuntimeInstance != null && s_activeRuntimeInstance != this)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (logLifecycleDiagnostics)
                        Debug.Log($"[SubnauticaSystemsDebugUI] Destroying duplicate runtime owner '{name}' id={EntityId.ToULong(GetEntityId())} active={gameObject.activeSelf}.", this);
#endif
                    Destroy(gameObject);
                    return;
                }

                s_activeRuntimeInstance = this;
            }

            if (Application.isPlaying && persistAcrossSceneLoads)
                GameBootstrapper.PersistRuntimeService(this);

            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logLifecycleDiagnostics)
                Debug.Log($"[SubnauticaSystemsDebugUI] Awake '{name}' id={EntityId.ToULong(GetEntityId())} persist={persistAcrossSceneLoads}.", this);
#endif
            QueueRuntimeBootstrap(forceManagerResolve: true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Application.isPlaying && logLifecycleDiagnostics)
                Debug.Log($"[SubnauticaSystemsDebugUI] OnEnable '{name}' id={EntityId.ToULong(GetEntityId())}.", this);
#endif
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            TryRegisterHotSwapListener();
            if (!_registered || !_slowTickRegistered)
                TryRegister();
            QueueRuntimeBootstrap(forceManagerResolve: true);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            ClearStressHarness();
            TryUnregisterHotSwapListener();
            TryUnregister();
            SetVisible(false);

            if (!Application.isPlaying && s_activeRuntimeInstance == this)
                s_activeRuntimeInstance = null;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Application.isPlaying && logLifecycleDiagnostics)
                Debug.Log($"[SubnauticaSystemsDebugUI] OnDestroy '{name}' id={EntityId.ToULong(GetEntityId())}.", this);
#endif
            if (s_activeRuntimeInstance == this)
                s_activeRuntimeInstance = null;
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && isActiveAndEnabled)
                TryRegister();
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registered)
            {
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            }

            if (_slowTickRegistered)
                return;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (!_slowTickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _slowTickRegistered = false;
        }

        public void Tick(float dt)
        {
            if (!Application.isPlaying)
                return;

            _refreshTimer += dt;
            if (_refreshTimer < refreshInterval)
                return;

            _refreshTimer = 0f;
            _diagnosticsRefreshPending = true;
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            ProcessPendingBootstrap();
            ResolveManagers(force: false);
            if (_diagnosticsRefreshPending)
            {
                _diagnosticsRefreshPending = false;
                RefreshDiagnostics();
            }
            ApplyStressHarness();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            debugSceneName = nextScene.IsValid() ? nextScene.name : MissingLabel;
            _runtimeSnapshotLogged = false;
            _runtimeSnapshotScene = string.Empty;
            InvalidateResolvedManagers();
            QueueRuntimeBootstrap(forceManagerResolve: true);
        }

        private void QueueRuntimeBootstrap(bool forceManagerResolve)
        {
            _bootstrapPending = true;
            _diagnosticsRefreshPending = true;
            _nextBootstrapAttemptTime = float.NegativeInfinity;
            if (forceManagerResolve)
                _forceManagerResolveOnBootstrap = true;
        }

        private void ProcessPendingBootstrap()
        {
            if (!_bootstrapPending)
                return;

            float now = Time.unscaledTime;
            if (now < _nextBootstrapAttemptTime)
                return;

            _nextBootstrapAttemptTime = now + 0.25f;
            BootstrapRuntimeOverlay();
        }

        private void BootstrapRuntimeOverlay()
        {
            if (s_isBootstrappingRuntimeOverlay)
                return;

            s_isBootstrappingRuntimeOverlay = true;
            try
            {
                EnsureCanvasResolved();
                EnsureVisualTree();
                ResolveManagers(force: _forceManagerResolveOnBootstrap);
                ApplyStressHarness();
                RefreshDiagnostics();
                _bootstrapPending = ResolveCanvas() == null || _root == null;
                _forceManagerResolveOnBootstrap = false;
            }
            finally
            {
                s_isBootstrappingRuntimeOverlay = false;
            }
        }

        private void EnsureCanvasResolved()
        {
            if (targetCanvas == null)
            {
                SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
                for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
                {
                    SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                    Canvas candidate = overlay != null ? overlay.TargetCanvas : null;
                    if (candidate == null)
                        continue;

                    if (candidate.name == "Suit_HUD_Canvas")
                    {
                        targetCanvas = candidate;
                        break;
                    }

                    if (targetCanvas == null)
                        targetCanvas = candidate;
                }
            }

            if (targetCanvas != null && _runtimeCanvas != null)
            {
                ResetVisualTreeState();
                Destroy(_runtimeCanvas.gameObject);
                _runtimeCanvas = null;
            }

            if (targetCanvas == null && _runtimeCanvas == null)
                _runtimeCanvas = ResolveOrCreateRuntimeCanvas();

            debugCanvasResolved = _runtimeCanvas != null || targetCanvas != null;
        }

        private void EnsureVisualTree()
        {
            Canvas canvas = ResolveCanvas();
            if (canvas == null)
                return;

            if (_root != null && _root.parent != canvas.transform)
                ResetVisualTreeState();

            if (_root == null)
            {
                Transform existing = canvas.transform.Find(RootObjectName);
                _root = existing as RectTransform;
            }

            if (_root != null && _titleValue == null)
            {
                Destroy(_root.gameObject);
                _root = null;
            }

            if (_root == null)
                BuildVisualTree();

            if (_root != null)
                _root.SetAsLastSibling();

            SetVisible(_root != null);
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

        private void BuildVisualTree()
        {
            Canvas canvas = ResolveCanvas();
            if (canvas == null)
                return;

            GameObject rootObject = new GameObject(RootObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            rootObject.transform.SetParent(canvas.transform, false);
            rootObject.TryGetComponent(out _root);
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = anchoredPosition;
            _root.sizeDelta = panelSize;

            rootObject.TryGetComponent(out Image background);
            background.color = new Color(0.02f, 0.07f, 0.10f, 0.94f);

            rootObject.TryGetComponent(out _canvasGroup);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            CreateLabel("HeaderLabel", "SUBNAUTICA SYSTEMS DEBUG", new Vector2(16f, -14f), new Vector2(172f, 20f), 12f, FontStyles.Bold);
            _titleValue = CreateValue("HeaderValue", new Vector2(224f, -14f), new Vector2(192f, 20f), 12f, FontStyles.Bold);

            CreateLabel("SceneLabel", "ACTIVE SCENE", new Vector2(16f, -48f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _sceneValue = CreateValue("SceneValue", new Vector2(224f, -48f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("BootstrapLabel", "BOOTSTRAP", new Vector2(16f, -70f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _bootstrapValue = CreateValue("BootstrapValue", new Vector2(224f, -70f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("TickCountsLabel", "TICK COUNTS", new Vector2(16f, -92f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _tickCountsValue = CreateValue("TickCountsValue", new Vector2(224f, -92f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("RenderScaleLabel", "RENDER SCALE", new Vector2(16f, -124f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _renderScaleValue = CreateValue("RenderScaleValue", new Vector2(224f, -124f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("RenderPressureLabel", "PRESSURE", new Vector2(16f, -146f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _renderPressureValue = CreateValue("RenderPressureValue", new Vector2(224f, -146f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("FaunaBiomeLabel", "FAUNA BIOME", new Vector2(16f, -178f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _faunaBiomeValue = CreateValue("FaunaBiomeValue", new Vector2(224f, -178f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("FaunaBiasLabel", "FAUNA BIAS", new Vector2(16f, -200f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _faunaBiasValue = CreateValue("FaunaBiasValue", new Vector2(224f, -200f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("FaunaLimitLabel", "FAUNA LIMITS", new Vector2(16f, -222f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _faunaLimitValue = CreateValue("FaunaLimitValue", new Vector2(224f, -222f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("MusicTensionLabel", "MUSIC TENSION", new Vector2(16f, -254f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _musicTensionValue = CreateValue("MusicTensionValue", new Vector2(224f, -254f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("MusicProfileLabel", "MUSIC PROFILE", new Vector2(16f, -276f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _musicProfileValue = CreateValue("MusicProfileValue", new Vector2(224f, -276f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("SoundscapeTierLabel", "SOUNDSCAPE", new Vector2(16f, -298f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _soundscapeTierValue = CreateValue("SoundscapeTierValue", new Vector2(224f, -298f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("UnderwaterBudgetLabel", "UNDERWATER FX", new Vector2(16f, -320f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _underwaterBudgetValue = CreateValue("UnderwaterBudgetValue", new Vector2(224f, -320f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("CameraBudgetLabel", "CAMERA FX", new Vector2(16f, -342f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _cameraBudgetValue = CreateValue("CameraBudgetValue", new Vector2(224f, -342f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);

            CreateLabel("StressLabel", "STRESS HARNESS", new Vector2(16f, -364f), new Vector2(172f, 18f), 10.5f, FontStyles.Bold);
            _stressValue = CreateValue("StressValue", new Vector2(224f, -364f), new Vector2(192f, 18f), 10.5f, FontStyles.Normal);
        }

        private void RefreshDiagnostics()
        {
            if (_titleValue == null ||
                _sceneValue == null ||
                _bootstrapValue == null ||
                _tickCountsValue == null ||
                _renderScaleValue == null ||
                _renderPressureValue == null ||
                _faunaBiomeValue == null ||
                _faunaBiasValue == null ||
                _faunaLimitValue == null ||
                _musicTensionValue == null ||
                _musicProfileValue == null ||
                _soundscapeTierValue == null ||
                _underwaterBudgetValue == null ||
                _cameraBudgetValue == null ||
                _stressValue == null)
            {
                return;
            }

            ResolveManagers(force: false);

            DynamicResolutionScaler scaler = _resolvedScaler;
            FaunaDirector fauna = _resolvedFaunaDirector;
            HectonMusicDirector music = _resolvedMusicDirector;
            SoundscapeSystem soundscape = _resolvedSoundscapeSystem;
            HectonUnderwaterVisuals underwaterVisuals = _resolvedUnderwaterVisuals;
            ICameraJuiceSystem cameraJuice = _resolvedCameraJuiceSystem;
            Scene activeScene = SceneManager.GetActiveScene();

            string titleLabel = enableStressTest ? "LIVE / FORCED PRESSURE" : "LIVE / PASSIVE";
            SetDynamicText(_titleValue, titleLabel, ref _lastTitleValue);

            string sceneLabel = activeScene.IsValid() ? activeScene.name : MissingLabel;
            SetDynamicText(_sceneValue, sceneLabel, ref _lastSceneValue);
            debugSceneName = sceneLabel;

            string bootstrapLabel = ResolveBootstrapLabel();
            SetDynamicText(_bootstrapValue, bootstrapLabel, ref _lastBootstrapValue);
            debugBootstrapState = bootstrapLabel;

            SetTickCountsText(
                _tickCountsValue,
                GlobalRegistry.Updatables.Count,
                GlobalRegistry.FixedTickables.Count,
                GlobalRegistry.SlowTickables.Count);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugTickCounts = "LIVE HUD";
#endif

            if (scaler != null)
            {
                _lastRenderScaleValue = string.Empty;
                SetFloatText(_renderScaleValue, _renderScaleBuffer, scaler.CurrentRenderScale, "0.00", ref _lastRenderScaleHash, ref _lastRenderScaleLength);
            }
            else
            {
                InvalidateDynamicBufferCache(ref _lastRenderScaleHash, ref _lastRenderScaleLength);
                SetDynamicText(_renderScaleValue, MissingLabel, ref _lastRenderScaleValue);
            }

            string pressureLabel = scaler != null ? scaler.DebugPressureStateLabel : MissingLabel;
            SetDynamicText(_renderPressureValue, pressureLabel, ref _lastRenderPressureValue);
            debugRenderPressure = pressureLabel;

            string faunaBiome = fauna != null ? fauna.DebugBiomeLabel : MissingLabel;
            SetDynamicText(_faunaBiomeValue, faunaBiome, ref _lastFaunaBiomeValue);
            debugFaunaBiome = faunaBiome;

            string faunaBias = fauna != null ? fauna.DebugEcologyBiasLabel : MissingLabel;
            SetDynamicText(_faunaBiasValue, faunaBias, ref _lastFaunaBiasValue);
            debugFaunaBias = faunaBias;

            if (fauna != null)
            {
                _lastFaunaLimitValue = string.Empty;
                SetFaunaLimitsText(
                    _faunaLimitValue,
                    fauna.DebugEffectiveSpawnsPerTick,
                    fauna.DebugEffectiveBiomeMaxCount,
                    fauna.DebugEffectiveGlobalMaxCount);
            }
            else
            {
                InvalidateDynamicBufferCache(ref _lastFaunaLimitHash, ref _lastFaunaLimitLength);
                SetDynamicText(_faunaLimitValue, MissingLabel, ref _lastFaunaLimitValue);
            }

            if (music != null)
            {
                _lastMusicTensionValue = string.Empty;
                SetFloatText(_musicTensionValue, _musicTensionBuffer, music.CurrentTension01, "0.00", ref _lastMusicTensionHash, ref _lastMusicTensionLength);
                string profileLabel = music.ActiveResolvedProfile != null ? music.ActiveResolvedProfile.name : MissingLabel;
                SetDynamicText(_musicProfileValue, profileLabel, ref _lastMusicProfileValue);
                debugMusicProfile = profileLabel;
            }
            else
            {
                InvalidateDynamicBufferCache(ref _lastMusicTensionHash, ref _lastMusicTensionLength);
                SetDynamicText(_musicTensionValue, MissingLabel, ref _lastMusicTensionValue);
                SetDynamicText(_musicProfileValue, MissingLabel, ref _lastMusicProfileValue);
                debugMusicProfile = MissingLabel;
            }

            string soundscapeLabel = soundscape != null ? ResolveSoundscapeLabel(soundscape.CurrentTier) : MissingLabel;
            SetDynamicText(_soundscapeTierValue, soundscapeLabel, ref _lastSoundscapeTierValue);
            debugSoundscapeTier = soundscapeLabel;

            bool hasUnderwaterBudget = false;
            if (underwaterVisuals != null)
            {
                hasUnderwaterBudget = SetBudgetTripletText(
                    _underwaterBudgetValue,
                    _underwaterBudgetBuffer,
                    'M',
                    underwaterVisuals.DebugAdaptiveMotesScale,
                    "0.00",
                    'B',
                    underwaterVisuals.DebugAdaptiveBubbleScale,
                    "0.00",
                    'E',
                    underwaterVisuals.DebugSuspendedMotesEmission,
                    "0.0",
                    ref _lastUnderwaterBudgetHash,
                    ref _lastUnderwaterBudgetLength);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                debugUnderwaterBudget = hasUnderwaterBudget ? "LIVE HUD" : MissingLabel;
#endif
            }
            else
            {
                InvalidateDynamicBufferCache(ref _lastUnderwaterBudgetHash, ref _lastUnderwaterBudgetLength);
                SetDynamicText(_underwaterBudgetValue, MissingLabel, ref _lastUnderwaterBudgetValue);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                debugUnderwaterBudget = MissingLabel;
#endif
            }

            bool hasCameraBudget = false;
            if (cameraJuice != null)
            {
                hasCameraBudget = SetBudgetTripletText(
                    _cameraBudgetValue,
                    _cameraBudgetBuffer,
                    'S',
                    cameraJuice.DebugAdaptiveShakeScale,
                    "0.00",
                    'F',
                    cameraJuice.DebugAdaptiveFOVScale,
                    "0.00",
                    'P',
                    cameraJuice.DebugAdaptivePostFxScale,
                    "0.00",
                    ref _lastCameraBudgetHash,
                    ref _lastCameraBudgetLength);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                debugCameraBudget = hasCameraBudget ? "LIVE HUD" : MissingLabel;
#endif
            }
            else
            {
                InvalidateDynamicBufferCache(ref _lastCameraBudgetHash, ref _lastCameraBudgetLength);
                SetDynamicText(_cameraBudgetValue, MissingLabel, ref _lastCameraBudgetValue);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                debugCameraBudget = MissingLabel;
#endif
            }

            if (enableStressTest)
            {
                SetStressHarnessText(_stressValue, forcedRenderScale, forcedFrameTimeMs);
                _lastStressValue = EnabledLabel;
            }
            else
            {
                InvalidateDynamicBufferCache(ref _lastStressHash, ref _lastStressLength);
                SetDynamicText(_stressValue, DisabledLabel, ref _lastStressValue);
            }

            TryEmitRuntimeSnapshot(
                activeScene,
                scaler,
                fauna,
                music,
                soundscape,
                hasUnderwaterBudget,
                hasCameraBudget,
                pressureLabel,
                faunaBiome,
                faunaBias);
        }

        private void ApplyStressHarness()
        {
            ResolveManagers(force: false);
            DynamicResolutionScaler scaler = _resolvedScaler;
            if (scaler == null)
                return;

            if (!enableStressTest)
            {
                ClearStressHarness();
                return;
            }

            scaler.SetDebugFrameTimeOverride(forcedFrameTimeMs);
            scaler.SetDebugRenderScaleOverride(forcedRenderScale);
            _stressApplied = true;
        }

        private void ClearStressHarness()
        {
            if (!_stressApplied)
                return;

            DynamicResolutionScaler scaler = _resolvedScaler != null ? _resolvedScaler : GlobalRegistry.DynamicResolution;
            if (scaler != null)
            {
                scaler.ClearDebugFrameTimeOverride();
                scaler.ClearDebugRenderScaleOverride();
            }

            _stressApplied = false;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.enabled = visible;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void ResetVisualTreeState()
        {
            _root = null;
            _canvasGroup = null;
            _titleValue = null;
            _sceneValue = null;
            _bootstrapValue = null;
            _tickCountsValue = null;
            _renderScaleValue = null;
            _renderPressureValue = null;
            _faunaBiomeValue = null;
            _faunaBiasValue = null;
            _faunaLimitValue = null;
            _musicTensionValue = null;
            _musicProfileValue = null;
            _soundscapeTierValue = null;
            _underwaterBudgetValue = null;
            _cameraBudgetValue = null;
            _stressValue = null;
            ResetDynamicTextCache();
        }

        private void ResetDynamicTextCache()
        {
            _lastTitleValue = string.Empty;
            _lastSceneValue = string.Empty;
            _lastBootstrapValue = string.Empty;
            _lastTickCountsValue = string.Empty;
            _lastRenderScaleValue = string.Empty;
            _lastRenderPressureValue = string.Empty;
            _lastFaunaBiomeValue = string.Empty;
            _lastFaunaBiasValue = string.Empty;
            _lastFaunaLimitValue = string.Empty;
            _lastMusicTensionValue = string.Empty;
            _lastMusicProfileValue = string.Empty;
            _lastSoundscapeTierValue = string.Empty;
            _lastUnderwaterBudgetValue = string.Empty;
            _lastCameraBudgetValue = string.Empty;
            _lastStressValue = string.Empty;
            InvalidateDynamicBufferCache(ref _lastTickCountsHash, ref _lastTickCountsLength);
            InvalidateDynamicBufferCache(ref _lastRenderScaleHash, ref _lastRenderScaleLength);
            InvalidateDynamicBufferCache(ref _lastFaunaLimitHash, ref _lastFaunaLimitLength);
            InvalidateDynamicBufferCache(ref _lastMusicTensionHash, ref _lastMusicTensionLength);
            InvalidateDynamicBufferCache(ref _lastUnderwaterBudgetHash, ref _lastUnderwaterBudgetLength);
            InvalidateDynamicBufferCache(ref _lastCameraBudgetHash, ref _lastCameraBudgetLength);
            InvalidateDynamicBufferCache(ref _lastStressHash, ref _lastStressLength);
        }

        private TextMeshProUGUI CreateLabel(string name, string text, Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles fontStyle)
        {
            TextMeshProUGUI label = CreateText(name, anchoredPos, size, fontSize, fontStyle);
            label.SetText(text);
            label.color = new Color(0.50f, 0.86f, 0.92f, 0.82f);
            return label;
        }

        private TextMeshProUGUI CreateValue(string name, Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles fontStyle)
        {
            TextMeshProUGUI value = CreateText(name, anchoredPos, size, fontSize, fontStyle);
            value.alignment = TextAlignmentOptions.TopRight;
            value.color = new Color(0.87f, 0.97f, 1f, 0.96f);
            return value;
        }

        private TextMeshProUGUI CreateText(string name, Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles fontStyle)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(_root, false);

            textObject.TryGetComponent(out RectTransform rectTransform);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPos;
            rectTransform.sizeDelta = size;

            textObject.TryGetComponent(out TextMeshProUGUI text);
            text.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private void ResolveManagers(bool force)
        {
            if (!Application.isPlaying)
                return;

            if (AllResolvedManagersReady())
                return;

            float now = Time.unscaledTime;
            if (!force && now < _nextManagerResolveAttemptTime)
                return;

            _nextManagerResolveAttemptTime = now + math.max(0.1f, managerResolveRetryInterval);

            if (_resolvedScaler == null)
            {
                _resolvedScaler = GlobalRegistry.DynamicResolution;
            }

            if (_resolvedFaunaDirector == null)
            {
                WorldRuntimeReferenceUtility.TryResolveFaunaDirector(ref _resolvedFaunaDirector);
                if (_resolvedFaunaDirector == null)
                    _resolvedFaunaDirector = FaunaDirector.ActiveRuntimeInstance;
            }

            if (_resolvedMusicDirector == null)
            {
                _resolvedMusicDirector = GlobalRegistry.MusicDirector;
            }

            if (_resolvedSoundscapeSystem == null)
            {
                _resolvedSoundscapeSystem = GlobalRegistry.Soundscape;
            }

            if (_resolvedUnderwaterVisuals == null)
            {
                _resolvedUnderwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;
            }

            if (_resolvedCameraJuiceSystem == null)
            {
                _resolvedCameraJuiceSystem = GlobalRegistry.CameraJuice;
            }
        }

        private bool AllResolvedManagersReady()
        {
            return _resolvedScaler != null &&
                   _resolvedFaunaDirector != null &&
                   _resolvedMusicDirector != null &&
                   _resolvedSoundscapeSystem != null &&
                   _resolvedUnderwaterVisuals != null &&
                   _resolvedCameraJuiceSystem != null;
        }

        private void InvalidateResolvedManagers()
        {
            _resolvedScaler = null;
            _resolvedFaunaDirector = null;
            _resolvedMusicDirector = null;
            _resolvedSoundscapeSystem = null;
            _resolvedUnderwaterVisuals = null;
            _resolvedCameraJuiceSystem = null;
            _nextManagerResolveAttemptTime = float.NegativeInfinity;
        }

        private Canvas ResolveCanvas()
        {
            if (targetCanvas != null)
                return targetCanvas;

            return _runtimeCanvas;
        }

        private Canvas ResolveOrCreateRuntimeCanvas()
        {
            Transform existing = transform.Find(CanvasObjectName);
            Canvas canvas = null;
            if (existing != null)
                existing.TryGetComponent(out canvas);
            if (canvas != null)
                return canvas;

            GameObject canvasObject = new GameObject(
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            canvasObject.TryGetComponent(out canvas);
            if (canvas == null)
                return null;

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;

            if (canvasObject.TryGetComponent(out CanvasScaler scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (canvasObject.TryGetComponent(out GraphicRaycaster raycaster))
                raycaster.enabled = false;

            return canvas;
        }

        private static void SetDynamicText(TMP_Text label, string value, ref string cache)
        {
            if (label == null)
                return;

            string safeValue = string.IsNullOrEmpty(value) ? MissingLabel : value;
            if (cache == safeValue)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                int length = math.min(safeValue.Length, lease.Buffer.Length);
                safeValue.CopyTo(0, lease.Buffer, 0, length);
                label.SetCharArray(lease.Buffer, 0, length);
                cache = safeValue;
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private void SetTickCountsText(TMP_Text label, int tickables, int fixedTickables, int slowTickables)
        {
            if (label == null)
                return;

            int index = 0;
            index = WriteLiteral(_tickCountsBuffer, index, "T ");
            index = WriteInt(_tickCountsBuffer, index, tickables);
            index = WriteLiteral(_tickCountsBuffer, index, " | F ");
            index = WriteInt(_tickCountsBuffer, index, fixedTickables);
            index = WriteLiteral(_tickCountsBuffer, index, " | S ");
            index = WriteInt(_tickCountsBuffer, index, slowTickables);
            ApplyDynamicBufferIfChanged(label, _tickCountsBuffer, index, ref _lastTickCountsHash, ref _lastTickCountsLength);
        }

        private static void SetFloatText(TMP_Text label, char[] buffer, float value, string format, ref int cachedHash, ref int cachedLength)
        {
            if (label == null || buffer == null)
                return;

            if (!value.TryFormat(buffer.AsSpan(), out int length, format))
                return;

            ApplyDynamicBufferIfChanged(label, buffer, length, ref cachedHash, ref cachedLength);
        }

        private void SetFaunaLimitsText(TMP_Text label, float burst, float biomeLimit, float globalLimit)
        {
            if (label == null)
                return;

            int index = 0;
            index = WriteLiteral(_faunaLimitsBuffer, index, "Burst ");
            index = WriteInt(_faunaLimitsBuffer, index, Mathf.RoundToInt(burst));
            index = WriteLiteral(_faunaLimitsBuffer, index, " | Biome ");
            index = WriteInt(_faunaLimitsBuffer, index, Mathf.RoundToInt(biomeLimit));
            index = WriteLiteral(_faunaLimitsBuffer, index, " | Global ");
            index = WriteInt(_faunaLimitsBuffer, index, Mathf.RoundToInt(globalLimit));
            ApplyDynamicBufferIfChanged(label, _faunaLimitsBuffer, index, ref _lastFaunaLimitHash, ref _lastFaunaLimitLength);
        }

        private static bool SetBudgetTripletText(
            TMP_Text label,
            char[] buffer,
            char prefix0,
            float value0,
            string format0,
            char prefix1,
            float value1,
            string format1,
            char prefix2,
            float value2,
            string format2,
            ref int cachedHash,
            ref int cachedLength)
        {
            if (label == null || buffer == null)
                return false;

            int index = 0;
            index = WriteBudgetEntry(buffer, index, prefix0, value0, format0);
            index = WriteLiteral(buffer, index, " | ");
            index = WriteBudgetEntry(buffer, index, prefix1, value1, format1);
            index = WriteLiteral(buffer, index, " | ");
            index = WriteBudgetEntry(buffer, index, prefix2, value2, format2);
            ApplyDynamicBufferIfChanged(label, buffer, index, ref cachedHash, ref cachedLength);
            return true;
        }

        private void SetStressHarnessText(TMP_Text label, float renderScale, float frameTimeMs)
        {
            if (label == null)
                return;

            int index = 0;
            index = WriteLiteral(_stressBuffer, index, "ACTIVE / RS ");
            if (!renderScale.TryFormat(_stressBuffer.AsSpan(index), out int renderLength, "0.00"))
                return;

            index += renderLength;
            index = WriteLiteral(_stressBuffer, index, " / ");
            if (!frameTimeMs.TryFormat(_stressBuffer.AsSpan(index), out int frameLength, "0.0"))
                return;

            index += frameLength;
            index = WriteLiteral(_stressBuffer, index, " MS");
            ApplyDynamicBufferIfChanged(label, _stressBuffer, index, ref _lastStressHash, ref _lastStressLength);
        }

        private static bool ApplyDynamicBufferIfChanged(TMP_Text label, char[] buffer, int length, ref int cachedHash, ref int cachedLength)
        {
            if (label == null || buffer == null)
                return false;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            int hash = ComputeCharHash(buffer, safeLength);
            if (cachedLength == safeLength && cachedHash == hash)
                return false;

            label.SetCharArray(buffer, 0, safeLength);
            cachedHash = hash;
            cachedLength = safeLength;
            return true;
        }

        private static void InvalidateDynamicBufferCache(ref int cachedHash, ref int cachedLength)
        {
            cachedHash = 0;
            cachedLength = -1;
        }

        private static int ComputeCharHash(char[] buffer, int length)
        {
            unchecked
            {
                int hash = (int)2166136261u;
                int safeLength = math.max(0, math.min(length, buffer != null ? buffer.Length : 0));
                for (int i = 0; i < safeLength; i++)
                    hash = (hash ^ buffer[i]) * 16777619;

                return hash ^ safeLength;
            }
        }

        private static int WriteBudgetEntry(char[] buffer, int index, char prefix, float value, string format)
        {
            if (buffer == null || index >= buffer.Length)
                return index;

            buffer[index++] = prefix;
            buffer[index++] = ' ';
            if (!value.TryFormat(buffer.AsSpan(index), out int length, format))
                return index;

            return index + length;
        }

        private static int WriteLiteral(char[] buffer, int index, string literal)
        {
            if (buffer == null || string.IsNullOrEmpty(literal))
                return index;

            int copyLength = Mathf.Min(literal.Length, buffer.Length - index);
            literal.AsSpan(0, copyLength).CopyTo(buffer.AsSpan(index, copyLength));
            return index + copyLength;
        }

        private static int WriteInt(char[] buffer, int index, int value)
        {
            if (buffer == null || index >= buffer.Length)
                return index;

            if (!value.TryFormat(buffer.AsSpan(index), out int length))
                return index;

            return index + length;
        }

        private static string ResolveSoundscapeLabel(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Surface:
                    return "SURFACE";
                case SoundscapeTier.Shallow:
                    return "SHALLOW";
                case SoundscapeTier.Twilight:
                    return "TWILIGHT";
                case SoundscapeTier.Darkness:
                    return "DARKNESS";
                case SoundscapeTier.Abyss:
                    return "ABYSS";
                case SoundscapeTier.DeepAbyss:
                    return "DEEP ABYSS";
                case SoundscapeTier.Thermal:
                    return "THERMAL";
                default:
                    return "UNKNOWN";
            }
        }

        private static string ResolveBootstrapLabel()
        {
            if (!GameBootstrapper.AreAllSystemsReady())
                return PendingLabel;

            if (GameBootstrapper.IsGameReady)
                return "WORLD READY";

            if (GameBootstrapper.HasActiveInstance)
                return "WORLD PRIME";

            return ReadyLabel;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void TryEmitRuntimeSnapshot(
            Scene activeScene,
            DynamicResolutionScaler scaler,
            FaunaDirector fauna,
            HectonMusicDirector music,
            SoundscapeSystem soundscape,
            bool hasUnderwaterBudget,
            bool hasCameraBudget,
            string pressureLabel,
            string faunaBiome,
            string faunaBias)
        {
            if (!activeScene.IsValid() ||
                !string.Equals(activeScene.name, "02_HECTON_WORLD", System.StringComparison.Ordinal) ||
                !AllResolvedManagersReady() ||
                scaler == null ||
                fauna == null ||
                music == null ||
                soundscape == null ||
                !IsSnapshotRuntimeReady(scaler, fauna, music, pressureLabel, faunaBiome, hasUnderwaterBudget, hasCameraBudget))
            {
                return;
            }

            if (_runtimeSnapshotLogged &&
                string.Equals(_runtimeSnapshotScene, activeScene.name, System.StringComparison.Ordinal))
            {
                return;
            }

            _runtimeSnapshotLogged = true;
            _runtimeSnapshotScene = activeScene.name;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "[SubnauticaSystemsDebugUI] runtime-snapshot " +
                "scene=" + activeScene.name +
                " ticks=" + debugTickCounts +
                " renderScale=" + FormatSnapshotNumber(scaler.CurrentRenderScale, "0.00") +
                " pressure=" + pressureLabel +
                " faunaBiome=" + faunaBiome +
                " faunaBias=" + faunaBias +
                " faunaCaps=" +
                FormatSnapshotNumber(fauna.DebugEffectiveSpawnsPerTick, "0") + "/" +
                FormatSnapshotNumber(fauna.DebugEffectiveBiomeMaxCount, "0") + "/" +
                FormatSnapshotNumber(fauna.DebugEffectiveGlobalMaxCount, "0") +
                " tension=" + FormatSnapshotNumber(music.CurrentTension01, "0.00") +
                " musicProfile=" + (music.ActiveResolvedProfile != null ? music.ActiveResolvedProfile.name : MissingLabel) +
                " soundscape=" + ResolveSoundscapeLabel(soundscape.CurrentTier) +
                " underwater=LIVE HUD" +
                " camera=LIVE HUD" +
                " stress=" + (enableStressTest ? EnabledLabel : DisabledLabel),
                this);
#endif
        }

        private static string FormatSnapshotNumber(float value, string format)
        {
            char[] buffer = s_runtimeSnapshotNumberBuffer;
            if (buffer == null || buffer.Length < 32)
            {
                buffer = new char[32]; // COLD ALLOC: char[32] — runtime snapshot numeric staging buffer — owner: SubnauticaSystemsDebugUI
                s_runtimeSnapshotNumberBuffer = buffer;
            }

            if (!ZeroGCFormatter.TryWriteFloat(value, format.AsSpan(), buffer.AsSpan(), out int length))
                length = 0;

            return new string(buffer, 0, length);
        }

        private bool IsSnapshotRuntimeReady(
            DynamicResolutionScaler scaler,
            FaunaDirector fauna,
            HectonMusicDirector music,
            string pressureLabel,
            string faunaBiome,
            bool hasUnderwaterBudget,
            bool hasCameraBudget)
        {
            if (scaler == null || fauna == null || music == null)
                return false;

            if (string.IsNullOrEmpty(pressureLabel) ||
                string.Equals(pressureLabel, MissingLabel, System.StringComparison.Ordinal) ||
                string.IsNullOrEmpty(faunaBiome) ||
                string.Equals(faunaBiome, MissingLabel, System.StringComparison.Ordinal) ||
                string.Equals(faunaBiome, "-1", System.StringComparison.Ordinal) ||
                !hasUnderwaterBudget ||
                !hasCameraBudget ||
                music.ActiveResolvedProfile == null)
            {
                return false;
            }

            if (enableStressTest &&
                math.abs(scaler.CurrentRenderScale - forcedRenderScale) > 0.02f)
            {
                return false;
            }

            return true;
        }
    }
}
