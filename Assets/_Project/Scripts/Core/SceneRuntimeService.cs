using System;
using System.Runtime.CompilerServices;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Physics;
using Hecton8.VFX;
using Hecton8.World;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Core
{
    /// <summary>
    /// Guarded scene transition owner for GlobalRegistry cleanup and bootstrap gating.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9940)]
    public sealed class SceneRuntimeService : MonoBehaviour, ISceneService, IUpdatable, IServiceHeartbeat
    {
        private const int SceneActivationWatchdogInitialFrames = 1200;
        private const int SceneActivationWatchdogRepeatFrames = 300;
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string TransitionOverlayRootName = "[SceneRuntimeService_TransitionOverlay]";
        private const string TransitionDitherShaderName = "Hecton8/UI/BlueNoiseDitherDissolve";
        private const float MainMenuCameraPanDurationSeconds = 2f;
        private const float MainMenuCameraPanDepth = 9f;
        private const float MainMenuCameraPanPitchDegrees = 16f;
        private const float TransitionDissolveSeconds = 3f;
        private const float AudioSnapshotDiveCrossfadeSeconds = 4f;
        private const float CinematicHeaveAmplitude = 0.18f;
        private const float CinematicHeaveFrequencyHz = 0.31f;
        private const float MainMenuUiSubmergePixels = 140f;
        private const float WorldDroneLoadDb = -40f;
        private const float WorldDroneRuntimeDb = -5f;
        private const float InputReclaimStartFov = 90f;
        private const float InputReclaimDurationSeconds = 1f;
        private const int TransitionOverlaySortingOrder = 32766;
        private const int TerminalBootBufferLength = 384;
        private const uint TerminalBootHashSalt = 0x9E3779B9u;
        private static readonly int _TransitionDitherProgressId = Shader.PropertyToID("_DitherProgress");
        private static readonly int _TransitionDitherColorId = Shader.PropertyToID("_Color");
        private static readonly int _TransitionBlueNoiseTextureId = Shader.PropertyToID("_BlueNoiseTex");
        private static readonly int _HectonFreezeFrameDitherId = Shader.PropertyToID("_HectonFreezeFrameDither");
        private static readonly Color _TransitionAbyssColor = new Color(0.002f, 0.004f, 0.009f, 1f);
        private static readonly Color _TerminalBootTextColor = new Color(0.38f, 0.84f, 0.88f, 0.82f);

        [Header("Audio Snapshots")]
        [SerializeField] private AudioMixerSnapshot mainMenuMusicSnapshot;
        [SerializeField] private AudioMixerSnapshot abyssalAmbientSnapshot;

        private static bool _suppressRuntimeClearForManagedUnload;
        private bool _isInitialized;
        private bool _registeredSceneService;
        private bool _registeredSceneCallbacks;
        private bool _registeredUpdatable;
        private bool _sceneLoadInFlight;
        private string _pendingSceneName;
        private AsyncOperation _pendingSceneLoadOperation;
        private int _gpuResidencyReadyFrame = -1;
        private bool _cinematicTransitionActive;
        private float _cinematicTransitionElapsed;
        private Camera _cinematicCamera;
        private Camera _configuredCinematicCamera;
        private CanvasGroup _configuredCinematicMenuGroup;
        private CanvasGroup _cinematicMenuGroup;
        private RectTransform _cinematicMenuRect;
        private Texture _configuredBlueNoiseTexture;
        private Vector3 _cinematicCameraStartPosition;
        private Quaternion _cinematicCameraStartRotation;
        private Vector2 _cinematicMenuStartAnchoredPosition;
        private float _cinematicMenuStartAlpha;
        private GameObject _transitionOverlayRoot;
        private CanvasGroup _transitionOverlayGroup;
        private Material _transitionDitherMaterial;
        private TMP_Text _terminalBootText;
        // COLD ALLOC: char[384] - transition terminal boot text buffer - owner: SceneRuntimeService
        private readonly char[] _terminalBootBuffer = new char[TerminalBootBufferLength];
        private uint _terminalBootSeed;
        private int _terminalBootLastFrame = -1;

        /// <summary>
        /// True once the service has registered itself into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <summary>
        /// True when bootstrap has completed and guarded transitions are allowed.
        /// </summary>
        public bool CanLoadScene => GameBootstrapper.IsBootstrapComplete;

        internal void ConfigureMainMenuCinematic(Camera menuCamera, Texture blueNoiseTexture)
        {
            ConfigureMainMenuCinematic(menuCamera, blueNoiseTexture, null);
        }

        internal void ConfigureMainMenuCinematic(Camera menuCamera, Texture blueNoiseTexture, CanvasGroup menuGroup)
        {
            _configuredCinematicCamera = menuCamera;
            _configuredBlueNoiseTexture = blueNoiseTexture;
            _configuredCinematicMenuGroup = menuGroup;
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live scene service instance.</returns>
        public static SceneRuntimeService EnsureRuntimeInstance()
        {
            SceneRuntimeService sceneRuntime = GlobalRegistry.SceneRuntime;
            if (sceneRuntime != null)
                return sceneRuntime;

            GameObject runtimeRoot = new GameObject("[SceneRuntimeService]");
            SceneRuntimeService sceneService = runtimeRoot.AddComponent<SceneRuntimeService>();
            GlobalRegistry.RegisterSceneRuntime(sceneService);
            return sceneService;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GlobalRegistry.ClearSceneRuntime(null);
            _suppressRuntimeClearForManagedUnload = false;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            GlobalRegistry.RegisterSceneRuntime(this);

            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
                return;
            }

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterSceneService();
            TryRegisterSceneCallbacks();
        }

        /// <summary>
        /// Performs a guarded scene transition after clearing registry state.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        public void LoadScene(string sceneName)
        {
            if (_sceneLoadInFlight)
            {
                LogSceneLoadRejectedInFlight(sceneName, _pendingSceneName);
                return;
            }

            _ = LoadSceneAsync(sceneName);
        }

        /// <inheritdoc />
        public async Awaitable LoadSceneAsync(string sceneName)
        {
            if (!CanLoadScene)
            {
                LogSceneLoadRejectedBootstrapIncomplete(sceneName);
                return;
            }

            if (_sceneLoadInFlight)
            {
                LogSceneLoadRejectedInFlight(sceneName, _pendingSceneName);
                return;
            }

            try
            {
                _sceneLoadInFlight = true;
                _pendingSceneName = sceneName;
                _gpuResidencyReadyFrame = -1;
                Scene previousScene = SceneManager.GetActiveScene();
                bool useCinematicTransition = ShouldUseMainMenuCinematicTransition(previousScene, sceneName);
                if (useCinematicTransition)
                    BeginMainMenuCinematicTransition();

                ClearRuntimeState();

                LoadSceneMode loadMode = useCinematicTransition ? LoadSceneMode.Additive : LoadSceneMode.Single;
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
                if (loadOperation == null)
                {
                    LogSceneLoadOperationMissing(sceneName);
                    return;
                }

                _pendingSceneLoadOperation = loadOperation;
                _pendingSceneLoadOperation.allowSceneActivation = false;
                int waitFrames = 0;
                int nextWatchdogFrame = SceneActivationWatchdogInitialFrames;

                while (Application.isPlaying && ReferenceEquals(GlobalRegistry.SceneRuntime, this) && !_pendingSceneLoadOperation.isDone)
                {
                    if (useCinematicTransition)
                        TickMainMenuCinematicTransition(Time.unscaledDeltaTime);

                    bool loadReady = _pendingSceneLoadOperation.progress >= 0.9f;
                    bool poolsReady = loadReady && ArePersistentWorldPoolsReadyForSceneActivation();
                    bool originStable = poolsReady && IsFloatingOriginStableForSceneActivation();
                    bool gpuResidencyReady = IsGpuResidencyReadyForSceneActivation(loadReady, poolsReady, originStable);

                    if (loadReady && poolsReady && originStable && gpuResidencyReady)
                    {
                        _pendingSceneLoadOperation.allowSceneActivation = true;
                    }
                    else if (waitFrames >= nextWatchdogFrame)
                    {
                        LogSceneActivationWatchdog(sceneName, _pendingSceneLoadOperation.progress, loadReady, poolsReady, originStable, gpuResidencyReady, waitFrames);
                        nextWatchdogFrame = waitFrames + SceneActivationWatchdogRepeatFrames;
                    }

                    waitFrames++;
                    await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }

                if (useCinematicTransition && Application.isPlaying && ReferenceEquals(GlobalRegistry.SceneRuntime, this))
                    await CompleteMainMenuCinematicTransitionAsync(previousScene, sceneName);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                EndMainMenuCinematicTransition();
                _sceneLoadInFlight = false;
                _pendingSceneName = null;
                _pendingSceneLoadOperation = null;
                _gpuResidencyReadyFrame = -1;
            }
        }

        /// <summary>
        /// Core-lane dispatcher hook required by the runtime registry contract.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
        }

        private void Awake()
        {
            SceneRuntimeService activeRuntime = GlobalRegistry.SceneRuntime;
            if (activeRuntime != null && activeRuntime != this)
            {
                Destroy(gameObject);
                return;
            }

        }

        private void OnEnable()
        {
            TryRegisterUpdatable();
            if (_isInitialized)
            {
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
        }

        private void OnDestroy()
        {
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
            _isInitialized = false;

            GlobalRegistry.ClearSceneRuntime(this);
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            if (_suppressRuntimeClearForManagedUnload)
                return;

            ClearRuntimeState();
        }

        private static void ClearRuntimeState()
        {
            GlobalRegistry.ClearRuntimeBuckets();
            ThreadSafeCommandQueue.Clear();

            if (GlobalRegistry.Physics != null)
                GlobalRegistry.Physics.ClearQueuedPackets();
            else
                PhysicsApplySystem.ClearQueuedPacketsStatic();

            if (GlobalRegistry.InteractionSignals != null)
                GlobalRegistry.InteractionSignals.ClearQueuedSignals();

            if (GlobalRegistry.Debris != null)
                GlobalRegistry.Debris.ClearActiveDebris();

            GlobalPhysicsStateManager.ClearRuntimeStateStatic();
        }

        private static bool ArePersistentWorldPoolsReadyForSceneActivation()
        {
            if (!GameBootstrapper.ArePreWarmAssetsReady)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            return registry.AreResidentWorldPrefabPoolsReady();
        }

        private static bool ShouldUseMainMenuCinematicTransition(Scene previousScene, string nextSceneName)
        {
            return previousScene.IsValid() &&
                   previousScene.isLoaded &&
                   string.Equals(previousScene.name, MainMenuSceneName, StringComparison.Ordinal) &&
                   !string.Equals(previousScene.name, nextSceneName, StringComparison.Ordinal);
        }

        private void BeginMainMenuCinematicTransition()
        {
            _cinematicTransitionActive = true;
            _cinematicTransitionElapsed = 0f;
            _cinematicCamera = _configuredCinematicCamera;
            _cinematicMenuGroup = _configuredCinematicMenuGroup;
            _cinematicMenuRect = _cinematicMenuGroup != null
                ? _cinematicMenuGroup.transform as RectTransform
                : null;
            if (_cinematicCamera != null)
            {
                Transform cameraTransform = _cinematicCamera.transform;
                _cinematicCameraStartPosition = cameraTransform.position;
                _cinematicCameraStartRotation = cameraTransform.rotation;
            }

            if (_cinematicMenuGroup != null)
            {
                _cinematicMenuStartAlpha = _cinematicMenuGroup.alpha;
                _cinematicMenuGroup.interactable = false;
                _cinematicMenuGroup.blocksRaycasts = false;
            }

            if (_cinematicMenuRect != null)
                _cinematicMenuStartAnchoredPosition = _cinematicMenuRect.anchoredPosition;

            ResetWorldEntryFreezeState();
            BeginAudioSnapshotDiveCrossfade();
            BeginWorldDroneCrossfade();
            EnsureTransitionOverlay();
            _terminalBootSeed = ComputeTerminalBootSeed(_pendingSceneName, Time.frameCount);
            _terminalBootLastFrame = -1;
            UpdateTerminalBootOverlay();
            if (_transitionOverlayGroup != null)
                _transitionOverlayGroup.alpha = 0f;
            SetTransitionDitherCoverage(1f);
        }

        private void TickMainMenuCinematicTransition(float unscaledDeltaTime)
        {
            if (!_cinematicTransitionActive)
                return;

            _cinematicTransitionElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float normalized = MainMenuCameraPanDurationSeconds > 0f
                ? Mathf.Clamp01(_cinematicTransitionElapsed / MainMenuCameraPanDurationSeconds)
                : 1f;
            float eased = SmoothStep01(normalized);

            ApplyCinematicCameraPose(eased, _cinematicTransitionElapsed);

            if (_transitionOverlayGroup != null)
                _transitionOverlayGroup.alpha = eased;
            if (_cinematicMenuGroup != null)
                _cinematicMenuGroup.alpha = Mathf.LerpUnclamped(_cinematicMenuStartAlpha, 0f, eased);
            if (_cinematicMenuRect != null)
                _cinematicMenuRect.anchoredPosition = Vector2.LerpUnclamped(
                    _cinematicMenuStartAnchoredPosition,
                    _cinematicMenuStartAnchoredPosition + (Vector2.down * MainMenuUiSubmergePixels),
                    eased);

            SetTransitionDitherCoverage(1f);
            UpdateTerminalBootOverlay();
        }

        private async Awaitable CompleteMainMenuCinematicTransitionAsync(Scene previousScene, string loadedSceneName)
        {
            Scene loadedScene = SceneManager.GetSceneByName(loadedSceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                SceneManager.SetActiveScene(loadedScene);

            if (previousScene.IsValid() && previousScene.isLoaded && !string.Equals(previousScene.name, loadedSceneName, StringComparison.Ordinal))
            {
                _suppressRuntimeClearForManagedUnload = true;
                try
                {
                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene);
                    while (unloadOperation != null && !unloadOperation.isDone)
                    {
                        await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                    }
                }
                finally
                {
                    _suppressRuntimeClearForManagedUnload = false;
                }
            }

            BeginInputReclaimInterpolation();
            ResetWorldEntryFreezeState();
            await DissolveTransitionOverlayAsync();
        }

        private async Awaitable DissolveTransitionOverlayAsync()
        {
            EnsureTransitionOverlay();
            if (_transitionOverlayGroup == null)
            {
                UpdateWorldDroneCrossfade(1f);
                return;
            }

            _transitionOverlayGroup.alpha = 1f;
            float elapsed = 0f;
            while (Application.isPlaying && elapsed < TransitionDissolveSeconds)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float normalized = TransitionDissolveSeconds > 0f
                    ? Mathf.Clamp01(elapsed / TransitionDissolveSeconds)
                    : 1f;
                float eased = SmoothStep01(normalized);
                ApplyCinematicCameraPose(1f, MainMenuCameraPanDurationSeconds + elapsed);
                SetTransitionDitherCoverage(1f - eased);
                UpdateTerminalBootOverlay();
                UpdateWorldDroneCrossfade(eased);
                if (_transitionDitherMaterial == null)
                    _transitionOverlayGroup.alpha = 1f - eased;

                await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
            }

            SetTransitionDitherCoverage(0f);
            UpdateWorldDroneCrossfade(1f);
            _transitionOverlayGroup.alpha = 0f;
        }

        private void ApplyCinematicCameraPose(float eased, float elapsedSeconds)
        {
            if (_cinematicCamera == null)
                return;

            Transform cameraTransform = _cinematicCamera.transform;
            Vector3 targetPosition = _cinematicCameraStartPosition + (Vector3.down * MainMenuCameraPanDepth);
            Quaternion targetRotation = _cinematicCameraStartRotation * Quaternion.Euler(MainMenuCameraPanPitchDegrees, 0f, 0f);
            float heave = Mathf.Sin(elapsedSeconds * Mathf.PI * 2f * CinematicHeaveFrequencyHz) *
                          CinematicHeaveAmplitude *
                          SmoothStep01(eased);
            Vector3 heaveOffset = (_cinematicCameraStartRotation * Vector3.up) * heave;
            cameraTransform.SetPositionAndRotation(
                Vector3.LerpUnclamped(_cinematicCameraStartPosition, targetPosition, eased) + heaveOffset,
                Quaternion.SlerpUnclamped(_cinematicCameraStartRotation, targetRotation, eased));
        }

        private void EndMainMenuCinematicTransition()
        {
            _cinematicTransitionActive = false;
            _cinematicTransitionElapsed = 0f;
            _cinematicCamera = null;
            _configuredCinematicCamera = null;
            _configuredCinematicMenuGroup = null;
            _cinematicMenuGroup = null;
            _cinematicMenuRect = null;
            _configuredBlueNoiseTexture = null;

            if (_transitionOverlayRoot != null)
                Destroy(_transitionOverlayRoot);

            _transitionOverlayRoot = null;
            _transitionOverlayGroup = null;
            _terminalBootText = null;
            _terminalBootLastFrame = -1;
            if (_transitionDitherMaterial != null)
                Destroy(_transitionDitherMaterial);
            _transitionDitherMaterial = null;
            ResetWorldEntryFreezeState();
        }

        private void BeginAudioSnapshotDiveCrossfade()
        {
            if (mainMenuMusicSnapshot != null)
                mainMenuMusicSnapshot.TransitionTo(0f);

            if (abyssalAmbientSnapshot != null)
                abyssalAmbientSnapshot.TransitionTo(AudioSnapshotDiveCrossfadeSeconds);
        }

        private static void ResetWorldEntryFreezeState()
        {
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;

            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, 0f);
        }

        private static void BeginWorldDroneCrossfade()
        {
            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudio)
            {
                spatialAudio.BeginWorldDroneTransition(
                    WorldDroneLoadDb,
                    WorldDroneRuntimeDb,
                    TransitionDissolveSeconds);
            }
        }

        private static void UpdateWorldDroneCrossfade(float normalized)
        {
            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudio)
                spatialAudio.SetWorldDroneTransitionProgress(normalized);
        }

        private static void BeginInputReclaimInterpolation()
        {
            CameraJuiceSystem cameraJuice = GlobalRegistry.CameraJuice;
            if (cameraJuice != null)
                cameraJuice.BeginInputReclaimFov(InputReclaimStartFov, InputReclaimDurationSeconds);
        }

        private void EnsureTransitionOverlay()
        {
            if (_transitionOverlayRoot != null)
                return;

            GameObject root = new GameObject(TransitionOverlayRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup)); // COLD ALLOC: GameObject[1] - scene transition blackout overlay - owner: SceneRuntimeService
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TransitionOverlaySortingOrder;

            _transitionOverlayGroup = root.GetComponent<CanvasGroup>();
            _transitionOverlayGroup.alpha = 0f;
            _transitionOverlayGroup.interactable = false;
            _transitionOverlayGroup.blocksRaycasts = true;

            GameObject imageRoot = new GameObject("DitherBlackout", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] - full-screen dither image - owner: SceneRuntimeService
            imageRoot.transform.SetParent(root.transform, false);
            RectTransform imageRect = imageRoot.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            Image image = imageRoot.GetComponent<Image>();
            image.raycastTarget = true;
            image.color = _TransitionAbyssColor;

            Shader ditherShader = Shader.Find(TransitionDitherShaderName);
            if (ditherShader != null)
            {
                _transitionDitherMaterial = new Material(ditherShader); // COLD ALLOC: Material[1] - blue-noise scene dissolve material - owner: SceneRuntimeService
                _transitionDitherMaterial.SetColor(_TransitionDitherColorId, _TransitionAbyssColor);
                _transitionDitherMaterial.SetFloat(_TransitionDitherProgressId, 1f);
                if (_configuredBlueNoiseTexture != null)
                    _transitionDitherMaterial.SetTexture(_TransitionBlueNoiseTextureId, _configuredBlueNoiseTexture);
                image.material = _transitionDitherMaterial;
            }

            GameObject terminalRoot = new GameObject("TerminalBootOverlay", typeof(RectTransform), typeof(TextMeshProUGUI)); // COLD ALLOC: GameObject[1] - zero-GC terminal boot overlay text - owner: SceneRuntimeService
            terminalRoot.transform.SetParent(root.transform, false);
            RectTransform terminalRect = terminalRoot.GetComponent<RectTransform>();
            terminalRect.anchorMin = Vector2.zero;
            terminalRect.anchorMax = Vector2.one;
            terminalRect.offsetMin = new Vector2(48f, 40f);
            terminalRect.offsetMax = new Vector2(-48f, -40f);

            TextMeshProUGUI terminalText = terminalRoot.GetComponent<TextMeshProUGUI>();
            terminalText.raycastTarget = false;
            terminalText.alignment = TextAlignmentOptions.BottomLeft;
            terminalText.textWrappingMode = TextWrappingModes.NoWrap;
            terminalText.fontSize = 17f;
            terminalText.color = _TerminalBootTextColor;
            _terminalBootText = terminalText;

            _transitionOverlayRoot = root;
        }

        private void UpdateTerminalBootOverlay()
        {
            if (_terminalBootText == null)
                return;

            int frame = Time.frameCount;
            if (_terminalBootLastFrame == frame)
                return;

            _terminalBootLastFrame = frame;
            Span<char> buffer = _terminalBootBuffer;
            int cursor = 0;
            AppendLiteral(buffer, ref cursor, "LOADING NEURAL INTERFACE...");
            AppendNewLine(buffer, ref cursor);
            AppendLiteral(buffer, ref cursor, "AUP SECTOR 0x");
            AppendHex8(buffer, ref cursor, MixTerminalBootHash(_terminalBootSeed, (uint)frame));
            AppendLiteral(buffer, ref cursor, " / 0x");
            AppendHex8(buffer, ref cursor, MixTerminalBootHash(_terminalBootSeed ^ 0xA53A9D2Du, (uint)(frame + 17)));
            AppendNewLine(buffer, ref cursor);
            AppendLiteral(buffer, ref cursor, "BOOT MASK 0x");
            AppendHex8(buffer, ref cursor, MixTerminalBootHash(_terminalBootSeed ^ 0xC2B2AE35u, (uint)(frame + 31)));
            AppendNewLine(buffer, ref cursor);
            AppendServiceHandle(buffer, ref cursor, "DISP", GlobalRegistry.Dispatcher);
            AppendServiceHandle(buffer, ref cursor, "TICK", GlobalRegistry.TickManager);
            AppendServiceHandle(buffer, ref cursor, "SCEN", GlobalRegistry.Scene);
            AppendServiceHandle(buffer, ref cursor, "PHYS", GlobalRegistry.Physics);
            AppendServiceHandle(buffer, ref cursor, "AUDI", GlobalRegistry.Audio);

            _terminalBootText.SetCharArray(_terminalBootBuffer, 0, cursor);
        }

        private static uint ComputeTerminalBootSeed(string sceneName, int frame)
        {
            uint hash = TerminalBootHashSalt ^ (uint)frame;
            if (sceneName == null)
                return MixTerminalBootHash(hash, 0u);

            for (int i = 0; i < sceneName.Length; i++)
            {
                hash ^= sceneName[i];
                hash *= 16777619u;
            }

            return MixTerminalBootHash(hash, 0xB5297A4Du);
        }

        private static uint MixTerminalBootHash(uint seed, uint value)
        {
            uint hash = seed ^ (value + TerminalBootHashSalt + (seed << 6) + (seed >> 2));
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        private static void AppendLiteral(Span<char> buffer, ref int cursor, string literal)
        {
            if (literal == null)
                return;

            for (int i = 0; i < literal.Length && cursor < buffer.Length; i++)
                buffer[cursor++] = literal[i];
        }

        private static void AppendNewLine(Span<char> buffer, ref int cursor)
        {
            if (cursor < buffer.Length)
                buffer[cursor++] = '\n';
        }

        private static void AppendHex8(Span<char> buffer, ref int cursor, uint value)
        {
            if (cursor >= buffer.Length)
                return;

            if (value.TryFormat(buffer.Slice(cursor), out int written, "X8"))
                cursor += written;
        }

        private static void AppendHex16(Span<char> buffer, ref int cursor, ulong value)
        {
            if (cursor >= buffer.Length)
                return;

            if (value.TryFormat(buffer.Slice(cursor), out int written, "X16"))
                cursor += written;
        }

        private static void AppendServiceHandle(Span<char> buffer, ref int cursor, string label, object service)
        {
            AppendLiteral(buffer, ref cursor, "SVC ");
            AppendLiteral(buffer, ref cursor, label);
            AppendLiteral(buffer, ref cursor, " 0x");
            if (service == null)
            {
                AppendLiteral(buffer, ref cursor, IntPtr.Size == 8 ? "0000000000000000" : "00000000");
                AppendNewLine(buffer, ref cursor);
                return;
            }

            IntPtr handle = new IntPtr(RuntimeHelpers.GetHashCode(service));
            if (IntPtr.Size == 8)
                AppendHex16(buffer, ref cursor, unchecked((ulong)handle.ToInt64()));
            else
                AppendHex8(buffer, ref cursor, unchecked((uint)handle.ToInt32()));
            AppendNewLine(buffer, ref cursor);
        }

        private void SetTransitionDitherCoverage(float coverage)
        {
            if (_transitionDitherMaterial == null)
                return;

            _transitionDitherMaterial.SetFloat(_TransitionDitherProgressId, Mathf.Clamp01(coverage));
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - (2f * value));
        }

        private static bool IsFloatingOriginStableForSceneActivation()
        {
            return !HectonFloatingOrigin.IsShiftInProgress &&
                   !HectonFloatingOrigin.IsPhysicsPausedForShift;
        }

        private bool IsGpuResidencyReadyForSceneActivation(bool loadReady, bool poolsReady, bool originStable)
        {
            if (!loadReady || !poolsReady || !originStable)
            {
                _gpuResidencyReadyFrame = -1;
                return false;
            }

            if (_gpuResidencyReadyFrame < 0)
            {
                _gpuResidencyReadyFrame = Time.frameCount + 1;
                return false;
            }

            return Time.frameCount >= _gpuResidencyReadyFrame;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneActivationWatchdog(
            string sceneName,
            float progress,
            bool loadReady,
            bool poolsReady,
            bool originStable,
            bool gpuResidencyReady,
            int waitFrames)
        {
            string blockedBy = GetSceneActivationBlockedReason(loadReady, poolsReady, originStable, gpuResidencyReady);
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' still blocked after {waitFrames} frames. Reason: {blockedBy}. Progress: {progress:0.00}.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedInFlight(string sceneName, string pendingSceneName)
        {
            Debug.LogWarning($"[SceneRuntimeService] Scene load '{sceneName}' rejected because '{pendingSceneName}' is already in flight.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedBootstrapIncomplete(string sceneName)
        {
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' rejected while bootstrap is incomplete.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadOperationMissing(string sceneName)
        {
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' failed to create an AsyncOperation.");
        }

        private static string GetSceneActivationBlockedReason(bool loadReady, bool poolsReady, bool originStable, bool gpuResidencyReady)
        {
            if (!loadReady)
                return "AsyncOperation progress below activation threshold";
            if (!poolsReady)
                return "PersistentWorldRegistry pools not ready";
            if (!originStable)
                return "floating origin shift in progress";
            if (!gpuResidencyReady)
                return "GPU residency settle frame pending";

            return "activation pending";
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterSceneService()
        {
            if (_registeredSceneService)
                return;

            GlobalRegistry.RegisterSceneService(this);
            _registeredSceneService = ReferenceEquals(GlobalRegistry.Scene, this);
        }

        private void TryUnregisterSceneService()
        {
            if (!_registeredSceneService)
                return;

            GlobalRegistry.UnregisterSceneService(this);
            _registeredSceneService = false;
        }

        private void TryRegisterSceneCallbacks()
        {
            if (_registeredSceneCallbacks)
                return;

            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _registeredSceneCallbacks = true;
        }

        private void TryUnregisterSceneCallbacks()
        {
            if (!_registeredSceneCallbacks)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _registeredSceneCallbacks = false;
        }
    }
}
