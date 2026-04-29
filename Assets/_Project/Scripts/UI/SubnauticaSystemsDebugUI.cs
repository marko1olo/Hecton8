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
    public sealed class SubnauticaSystemsDebugUI : MonoBehaviour, ITickable, IUpdatable, ISlowTickable
    {
        // COLD ALLOC: List<SuitHUDV4CanvasOverlay>(4) â€” overlay canvas resolution buffer â€” owner: SubnauticaSystemsDebugUI
        private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer = new List<SuitHUDV4CanvasOverlay>(4);
        private static SubnauticaSystemsDebugUI s_activeRuntimeInstance;
        private static bool s_isBootstrappingRuntimeOverlay;

        internal static SubnauticaSystemsDebugUI ActiveRuntimeInstance => s_activeRuntimeInstance;

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
        private bool _stressApplied;
        private float _refreshTimer;
        private float _nextManagerResolveAttemptTime = float.NegativeInfinity;
        private DynamicResolutionScaler _resolvedScaler;
        private FaunaDirector _resolvedFaunaDirector;
        private HectonMusicDirector _resolvedMusicDirector;
        private SoundscapeSystem _resolvedSoundscapeSystem;
        private HectonUnderwaterVisuals _resolvedUnderwaterVisuals;
        private CameraJuiceSystem _resolvedCameraJuiceSystem;
        private string _lastSceneValue = string.Empty;
        private string _lastBootstrapValue = string.Empty;
        private string _lastTickCountsValue = string.Empty;
        private string _lastRenderPressureValue = string.Empty;
        private string _lastFaunaBiomeValue = string.Empty;
        private string _lastFaunaBiasValue = string.Empty;
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

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (s_activeRuntimeInstance != null && s_activeRuntimeInstance != this)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[SubnauticaSystemsDebugUI] Destroying duplicate runtime owner '{name}' id={EntityId.ToULong(GetEntityId())} active={gameObject.activeSelf}.", this);
#endif
                    Destroy(gameObject);
                    return;
                }

                s_activeRuntimeInstance = this;
            }

            if (Application.isPlaying && persistAcrossSceneLoads)
                DontDestroyOnLoad(gameObject);

            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SubnauticaSystemsDebugUI] Awake '{name}' id={EntityId.ToULong(GetEntityId())} persist={persistAcrossSceneLoads}.", this);
#endif
            QueueRuntimeBootstrap(forceManagerResolve: true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Application.isPlaying)
                Debug.Log($"[SubnauticaSystemsDebugUI] OnEnable '{name}' id={EntityId.ToULong(GetEntityId())}.", this);
#endif
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            TryRegister();
            QueueRuntimeBootstrap(forceManagerResolve: true);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            ClearStressHarness();
            TryUnregister();
            SetVisible(false);

            if (!Application.isPlaying && s_activeRuntimeInstance == this)
                s_activeRuntimeInstance = null;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Application.isPlaying)
                Debug.Log($"[SubnauticaSystemsDebugUI] OnDestroy '{name}' id={EntityId.ToULong(GetEntityId())}.", this);
#endif
            if (s_activeRuntimeInstance == this)
                s_activeRuntimeInstance = null;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registered = true;
            }

            if (_slowTickRegistered)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _slowTickRegistered = true;
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

            TryRegister();

            _refreshTimer += dt;
            if (_refreshTimer < refreshInterval)
                return;

            _refreshTimer = 0f;
            RefreshDiagnostics();
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            TryRegister();
            ProcessPendingBootstrap();
            ResolveManagers(force: false);
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

        private void BuildVisualTree()
        {
            Canvas canvas = ResolveCanvas();
            if (canvas == null)
                return;

            GameObject rootObject = new GameObject(RootObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            rootObject.transform.SetParent(canvas.transform, false);
            _root = rootObject.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = anchoredPosition;
            _root.sizeDelta = panelSize;

            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.02f, 0.07f, 0.10f, 0.94f);

            _canvasGroup = rootObject.GetComponent<CanvasGroup>();
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
            CameraJuiceSystem cameraJuice = _resolvedCameraJuiceSystem;
            Scene activeScene = SceneManager.GetActiveScene();

            _titleValue.SetText(enableStressTest ? "LIVE / FORCED PRESSURE" : "LIVE / PASSIVE");

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
                SetFloatText(_renderScaleValue, _renderScaleBuffer, scaler.CurrentRenderScale, "0.00");
            }
            else
            {
                _renderScaleValue.SetText(MissingLabel);
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
                SetFaunaLimitsText(
                    _faunaLimitValue,
                    fauna.DebugEffectiveSpawnsPerTick,
                    fauna.DebugEffectiveBiomeMaxCount,
                    fauna.DebugEffectiveGlobalMaxCount);
            }
            else
            {
                _faunaLimitValue.SetText(MissingLabel);
            }

            if (music != null)
            {
                SetFloatText(_musicTensionValue, _musicTensionBuffer, music.CurrentTension01, "0.00");
                string profileLabel = music.ActiveResolvedProfile != null ? music.ActiveResolvedProfile.name : MissingLabel;
                SetDynamicText(_musicProfileValue, profileLabel, ref _lastMusicProfileValue);
                debugMusicProfile = profileLabel;
            }
            else
            {
                _musicTensionValue.SetText(MissingLabel);
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
                    "0.0");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                debugUnderwaterBudget = hasUnderwaterBudget ? "LIVE HUD" : MissingLabel;
#endif
            }
            else
            {
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
                    "0.00");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                debugCameraBudget = hasCameraBudget ? "LIVE HUD" : MissingLabel;
#endif
            }
            else
            {
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

            DynamicResolutionScaler scaler = _resolvedScaler != null ? _resolvedScaler : DynamicResolutionScaler.Instance;
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
            _lastSceneValue = string.Empty;
            _lastBootstrapValue = string.Empty;
            _lastTickCountsValue = string.Empty;
            _lastRenderPressureValue = string.Empty;
            _lastFaunaBiomeValue = string.Empty;
            _lastFaunaBiasValue = string.Empty;
            _lastMusicProfileValue = string.Empty;
            _lastSoundscapeTierValue = string.Empty;
            _lastUnderwaterBudgetValue = string.Empty;
            _lastCameraBudgetValue = string.Empty;
            _lastStressValue = string.Empty;
        }

        private TextMeshProUGUI CreateLabel(string name, string text, Vector2 anchoredPos, Vector2 size, float fontSize, FontStyles fontStyle)
        {
            TextMeshProUGUI label = CreateText(name, anchoredPos, size, fontSize, fontStyle);
            label.text = text;
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

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPos;
            rectTransform.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
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

            _nextManagerResolveAttemptTime = now + Mathf.Max(0.1f, managerResolveRetryInterval);

            if (_resolvedScaler == null)
            {
                _resolvedScaler = DynamicResolutionScaler.Instance;
                if (_resolvedScaler == null)
                    _resolvedScaler = DynamicResolutionScaler.Instance;
            }

            if (_resolvedFaunaDirector == null)
            {
                WorldRuntimeReferenceUtility.TryResolveFaunaDirector(ref _resolvedFaunaDirector);
                if (_resolvedFaunaDirector == null)
                    _resolvedFaunaDirector = FaunaDirector.ActiveRuntimeInstance;
            }

            if (_resolvedMusicDirector == null)
            {
                if (!HectonMusicDirector.TryGetInstance(out _resolvedMusicDirector))
                    _resolvedMusicDirector = HectonMusicDirector.Instance;
            }

            if (_resolvedSoundscapeSystem == null)
            {
                _resolvedSoundscapeSystem = SoundscapeSystem.Instance;
                if (_resolvedSoundscapeSystem == null)
                    _resolvedSoundscapeSystem = SoundscapeSystem.Instance;
            }

            if (_resolvedUnderwaterVisuals == null)
            {
                _resolvedUnderwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;
                if (_resolvedUnderwaterVisuals == null)
                    _resolvedUnderwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;
            }

            if (_resolvedCameraJuiceSystem == null)
            {
                _resolvedCameraJuiceSystem = CameraJuiceSystem.Instance;
                if (_resolvedCameraJuiceSystem == null)
                    _resolvedCameraJuiceSystem = CameraJuiceSystem.Instance;
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
            Canvas canvas = existing != null ? existing.GetComponent<Canvas>() : null;
            if (canvas != null)
                return canvas;

            GameObject canvasObject = new GameObject(
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            return canvas;
        }

        private static void SetDynamicText(TMP_Text label, string value, ref string cache)
        {
            string safeValue = string.IsNullOrEmpty(value) ? MissingLabel : value;
            if (cache == safeValue)
                return;

            cache = safeValue;
            label.SetText(safeValue);
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
            ApplyDynamicBuffer(label, _tickCountsBuffer, index);
        }

        private static void SetFloatText(TMP_Text label, char[] buffer, float value, string format)
        {
            if (label == null || buffer == null)
                return;

            if (!value.TryFormat(buffer.AsSpan(), out int length, format))
                return;

            ApplyDynamicBuffer(label, buffer, length);
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
            ApplyDynamicBuffer(label, _faunaLimitsBuffer, index);
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
            string format2)
        {
            if (label == null || buffer == null)
                return false;

            int index = 0;
            index = WriteBudgetEntry(buffer, index, prefix0, value0, format0);
            index = WriteLiteral(buffer, index, " | ");
            index = WriteBudgetEntry(buffer, index, prefix1, value1, format1);
            index = WriteLiteral(buffer, index, " | ");
            index = WriteBudgetEntry(buffer, index, prefix2, value2, format2);
            ApplyDynamicBuffer(label, buffer, index);
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
            ApplyDynamicBuffer(label, _stressBuffer, index);
        }

        private static void ApplyDynamicBuffer(TMP_Text label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
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
            if (!BootstrapController.AreAllSystemsReady())
                return PendingLabel;

            if (SceneBootstrap.IsGameReady)
                return "WORLD READY";

            if (SceneBootstrap.HasActiveInstance)
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

            Debug.Log(
                "[SubnauticaSystemsDebugUI] runtime-snapshot " +
                "scene=" + activeScene.name +
                " ticks=" + debugTickCounts +
                " renderScale=" + scaler.CurrentRenderScale.ToString("0.00") +
                " pressure=" + pressureLabel +
                " faunaBiome=" + faunaBiome +
                " faunaBias=" + faunaBias +
                " faunaCaps=" +
                fauna.DebugEffectiveSpawnsPerTick.ToString("0") + "/" +
                fauna.DebugEffectiveBiomeMaxCount.ToString("0") + "/" +
                fauna.DebugEffectiveGlobalMaxCount.ToString("0") +
                " tension=" + music.CurrentTension01.ToString("0.00") +
                " musicProfile=" + (music.ActiveResolvedProfile != null ? music.ActiveResolvedProfile.name : MissingLabel) +
                " soundscape=" + ResolveSoundscapeLabel(soundscape.CurrentTier) +
                " underwater=LIVE HUD" +
                " camera=LIVE HUD" +
                " stress=" + (enableStressTest ? EnabledLabel : DisabledLabel),
                this);
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
                Mathf.Abs(scaler.CurrentRenderScale - forcedRenderScale) > 0.02f)
            {
                return false;
            }

            return true;
        }
    }
}
