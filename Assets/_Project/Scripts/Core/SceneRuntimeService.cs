using System;
using Hecton8.Bootstrap;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using TMPro;
using Unity.Mathematics;
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
    public sealed class SceneRuntimeService : MonoBehaviour, ISceneService, IUpdatable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int SceneActivationWatchdogInitialFrames = 1200;
        private const int SceneActivationWatchdogRepeatFrames = 300;
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const string TransitionOverlayRootName = "[SceneRuntimeService_TransitionOverlay]";
        private const string TransitionDitherShaderName = "Hecton8/UI/IGNDitherDissolve";
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
        private const double TransitionSolveTelemetryThresholdMs = 0.2d;
        private const int TransitionOverlaySortingOrder = 32766;
        private const int TerminalBootBufferLength = 384;
        private const uint TerminalBootHashSalt = 0x9E3779B9u;
        private const uint TransitionSolveBudgetWarningHash = 0x54534F4Cu; // TSOL
        private const uint TransitionTelemetryContextHash = 0x53434E45u; // SCNE
        private const uint MemoryTransitionPauseSourceHash = 0x4D454D50u; // MEMP
        private const byte MemoryTransitionLockFlag = 1 << 0;
        private const byte MemoryTransitionReleasedFlag = 1 << 1;
        private const byte MemoryTransitionFailedFlag = 1 << 2;
        private const byte MemoryTransitionVaultBlockedFlag = 1 << 3;
        private const byte MemoryTransitionVaultLockedFlag = 1 << 4;
        private static readonly int _TransitionDitherProgressId = Shader.PropertyToID("_DitherProgress");
        private static readonly int _TransitionDitherColorId = Shader.PropertyToID("_Color");
        private static readonly int _HectonFreezeFrameDitherId = Shader.PropertyToID("_HectonFreezeFrameDither");
        private static readonly int _GamePausedId = Shader.PropertyToID("_GamePaused");
        private static readonly Color _TransitionAbyssColor = new Color(0.002f, 0.004f, 0.009f, 1f);
        private static readonly Color _TerminalBootTextColor = new Color(0.38f, 0.84f, 0.88f, 0.82f);
        private static readonly byte[] _TerminalBootNeuralInterfaceBytes =
        {
            (byte)'L', (byte)'O', (byte)'A', (byte)'D', (byte)'I', (byte)'N', (byte)'G', (byte)' ',
            (byte)'N', (byte)'E', (byte)'U', (byte)'R', (byte)'A', (byte)'L', (byte)' ',
            (byte)'I', (byte)'N', (byte)'T', (byte)'E', (byte)'R', (byte)'F', (byte)'A',
            (byte)'C', (byte)'E', (byte)'.', (byte)'.', (byte)'.',
        };
        private static readonly byte[] _TerminalBootAupSectorBytes =
        {
            (byte)'A', (byte)'U', (byte)'P', (byte)' ', (byte)'S', (byte)'E', (byte)'C',
            (byte)'T', (byte)'O', (byte)'R', (byte)' ', (byte)'0', (byte)'x',
        };
        private static readonly byte[] _TerminalBootSectorSeparatorBytes = { (byte)' ', (byte)'/', (byte)' ', (byte)'0', (byte)'x' };
        private static readonly byte[] _TerminalBootMaskBytes =
        {
            (byte)'B', (byte)'O', (byte)'O', (byte)'T', (byte)' ', (byte)'M', (byte)'A',
            (byte)'S', (byte)'K', (byte)' ', (byte)'0', (byte)'x',
        };
        private static readonly byte[] _TerminalBootServicePrefixBytes = { (byte)'S', (byte)'V', (byte)'C', (byte)' ' };
        private static readonly byte[] _TerminalBootServiceHandlePrefixBytes = { (byte)' ', (byte)'0', (byte)'x' };
        private static readonly byte[] _TerminalBootZeroHandle32Bytes =
        {
            (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0',
        };
        private static readonly byte[] _TerminalBootZeroHandle64Bytes =
        {
            (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0',
            (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0',
        };
        private static readonly byte[] _TerminalBootDispatcherLabelBytes = { (byte)'D', (byte)'I', (byte)'S', (byte)'P' };
        private static readonly byte[] _TerminalBootTickLabelBytes = { (byte)'T', (byte)'I', (byte)'C', (byte)'K' };
        private static readonly byte[] _TerminalBootSceneLabelBytes = { (byte)'S', (byte)'C', (byte)'E', (byte)'N' };
        private static readonly byte[] _TerminalBootPhysicsLabelBytes = { (byte)'P', (byte)'H', (byte)'Y', (byte)'S' };
        private static readonly byte[] _TerminalBootAudioLabelBytes = { (byte)'A', (byte)'U', (byte)'D', (byte)'I' };

        [Header("Audio Snapshots")]
        [SerializeField] private AudioMixerSnapshot mainMenuMusicSnapshot;
        [SerializeField] private AudioMixerSnapshot abyssalAmbientSnapshot;

        private static bool _suppressRuntimeClearForManagedUnload;
        private bool _isInitialized;
        private bool _registeredSceneService;
        private bool _registeredSceneCallbacks;
        private bool _registeredUpdatable;
        private bool _registeredHotSwapListener;
        private bool _sceneLoadInFlight;
        private string _pendingSceneName;
        private AsyncOperation _pendingSceneLoadOperation;
        private int _gpuResidencyReadyFrame = -1;
        private bool _sceneActivationReleased;
        private bool _cinematicTransitionActive;
        private bool _memoryLifecycleTransitionActive;
        private bool _memoryLifecycleSceneUnloadObserved;
        private IDataVault _dataVault;
        private uint _memoryLifecyclePauseSequence;
        private int _lastSceneOwnedVaultRemainingCount;
        private int _lastSceneOwnedVaultLockedCount;
        private long _lastSceneOwnedVaultRemainingBytes;
        private float _cinematicTransitionElapsed;
        private Camera _cinematicCamera;
        private Camera _configuredCinematicCamera;
        private CanvasGroup _configuredCinematicMenuGroup;
        private CanvasGroup _cinematicMenuGroup;
        private RectTransform _cinematicMenuRect;
        private Vector3 _cinematicCameraStartPosition;
        private Vector3 _cinematicCameraTargetPosition;
        private Vector3 _cinematicCameraTargetDelta;
        private Quaternion _cinematicCameraStartRotation;
        private Quaternion _cinematicCameraTargetRotation;
        private Vector2 _cinematicMenuStartAnchoredPosition;
        private Vector2 _cinematicMenuTargetAnchoredPosition;
        private float _cinematicMenuStartAlpha;
        private GameObject _transitionOverlayRoot;
        private CanvasGroup _transitionOverlayGroup;
        private Material _transitionDitherMaterial;
        private TMP_Text _terminalBootText;
        // COLD ALLOC: char[384] - transition terminal boot text buffer - owner: SceneRuntimeService
        private readonly char[] _terminalBootBuffer = new char[TerminalBootBufferLength];
        private uint _terminalBootSeed;
        private int _terminalBootLastFrame = -1;
        private bool _transitionPerformanceWarningPublished;

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

        internal static void ReleaseSceneActivation(AsyncOperation operation)
        {
            if (operation == null)
                return;

            operation.allowSceneActivation = true;
        }

        internal void ConfigureMainMenuCinematic(Camera menuCamera)
        {
            ConfigureMainMenuCinematic(menuCamera, null);
        }

        internal void ConfigureMainMenuCinematic(Camera menuCamera, CanvasGroup menuGroup)
        {
            _configuredCinematicCamera = menuCamera;
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
            H8Memory.Initialize();
            _dataVault = GlobalRegistry.DataVault;
            TryRegisterHotSwapListener();

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
                _sceneActivationReleased = false;
                Scene previousScene = SceneManager.GetActiveScene();
                bool useCinematicTransition = ShouldUseMainMenuCinematicTransition(previousScene, sceneName);
                if (useCinematicTransition)
                    BeginMainMenuCinematicTransition();

                BeginMemoryLifecycleTransition();
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
                        TickMainMenuCinematicTransition(ResolveTransitionUnscaledDeltaTime());

                    bool loadReady = _pendingSceneLoadOperation.progress >= 0.9f;
                    bool requiresWorldResidencyGate = RequiresWorldResidencyGate(sceneName);
                    bool poolsReady = !requiresWorldResidencyGate || (loadReady && ArePersistentWorldPoolsReadyForSceneActivation());
                    bool originStable = !requiresWorldResidencyGate || (poolsReady && IsFloatingOriginStableForSceneActivation());
                    bool gpuResidencyReady = !requiresWorldResidencyGate ||
                                             IsGpuResidencyReadyForSceneActivation(loadReady, poolsReady, originStable);

                    if (!_sceneActivationReleased &&
                        IsSceneActivationGateReady(useCinematicTransition, loadReady, poolsReady, originStable, gpuResidencyReady))
                    {
                        ReleaseSceneActivation(_pendingSceneLoadOperation);
                        _sceneActivationReleased = true;
                    }
                    else if (waitFrames >= nextWatchdogFrame)
                    {
                        LogSceneActivationWatchdog(sceneName, _pendingSceneLoadOperation.progress, loadReady, poolsReady, originStable, gpuResidencyReady, waitFrames);
                        nextWatchdogFrame = waitFrames + SceneActivationWatchdogRepeatFrames;
                    }

                    waitFrames++;
                    await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
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
                CompleteMemoryLifecycleTransitionAfterLoadAttempt();
                _sceneLoadInFlight = false;
                _pendingSceneName = null;
                _pendingSceneLoadOperation = null;
                _gpuResidencyReadyFrame = -1;
                _sceneActivationReleased = false;
            }
        }

        /// <summary>
        /// Core-lane dispatcher hook required by the runtime registry contract.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            H8Memory.RecordHeartbeat();
            if (_dataVault != null)
                _dataVault.RecordHeartbeat();
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
            TryRegisterHotSwapListener();
            if (_isInitialized)
            {
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_memoryLifecycleTransitionActive)
                CancelMemoryLifecycleTransition();

            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
            EndMainMenuCinematicTransition();
            _sceneLoadInFlight = false;
            _pendingSceneName = null;
            _pendingSceneLoadOperation = null;
            _gpuResidencyReadyFrame = -1;
            _sceneActivationReleased = false;
            _dataVault = null;
            _isInitialized = false;

            GlobalRegistry.ClearSceneRuntime(this);
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            if (!_suppressRuntimeClearForManagedUnload)
                ClearRuntimeState();

            SceneRuntimeService runtime = GlobalRegistry.SceneRuntime;
            if (runtime != null)
            {
                runtime._memoryLifecycleSceneUnloadObserved = true;
                runtime.CompleteMemoryLifecycleTransition();
            }
        }

        private static void ClearRuntimeState()
        {
            GlobalRegistry.ClearRuntimeBuckets();
            ThreadSafeCommandQueue.Clear();

            if (GlobalRegistry.Physics is ISceneTransitionPhysicsBridge scenePhysics)
                scenePhysics.ClearSceneTransitionRuntimeState();
            else if (GlobalRegistry.Physics != null)
                GlobalRegistry.Physics.ClearQueuedPackets();

            if (GlobalRegistry.InteractionSignals != null)
                GlobalRegistry.InteractionSignals.ClearQueuedSignals();

            if (GlobalRegistry.DebrisCompute != null)
                GlobalRegistry.DebrisCompute.ClearGpuDebris();

            SceneRuntimeService runtime = GlobalRegistry.SceneRuntime;
            if (runtime != null)
                runtime.RestoreCoreTickAfterRuntimeStateClear();
        }

        private void BeginMemoryLifecycleTransition()
        {
            CacheDataVaultCold();
            H8Memory.BeginSceneTransitionPurge();
            H8Memory.SetSceneUnloadedVerificationDeferred(true);
            _memoryLifecycleTransitionActive = true;
            _memoryLifecycleSceneUnloadObserved = false;
            PublishMemoryLifecyclePause(paused: true, MemoryTransitionLockFlag);
        }

        private void CompleteMemoryLifecycleTransitionAfterLoadAttempt()
        {
            if (!_memoryLifecycleTransitionActive)
                return;
            if (_memoryLifecycleSceneUnloadObserved)
            {
                CompleteMemoryLifecycleTransition();
                return;
            }

            CancelMemoryLifecycleTransition();
        }

        private void CompleteMemoryLifecycleTransition()
        {
            if (!_memoryLifecycleTransitionActive)
                return;
            if (!_memoryLifecycleSceneUnloadObserved)
                return;

            bool vaultVerified = ReleaseSceneOwnedVaultBuffers();
            bool verified = H8Memory.CompleteSceneTransitionVerification();
            if (verified && vaultVerified)
            {
                H8Memory.SetSceneUnloadedVerificationDeferred(false);
                PublishMemoryLifecyclePause(paused: false, MemoryTransitionReleasedFlag);
                _memoryLifecycleTransitionActive = false;
                _memoryLifecycleSceneUnloadObserved = false;
                return;
            }

            byte failureFlags = MemoryTransitionFailedFlag;
            if (_lastSceneOwnedVaultRemainingCount > 0)
                failureFlags |= MemoryTransitionVaultBlockedFlag;
            if (_lastSceneOwnedVaultLockedCount > 0)
                failureFlags |= MemoryTransitionVaultLockedFlag;
            PublishMemoryLifecyclePause(paused: true, failureFlags);
            GlobalTelemetryBus.PublishMemoryBreachEvent(
                MemoryTransitionPauseSourceHash,
                (H8Memory.TotalAllocatedBytes + _lastSceneOwnedVaultRemainingBytes) * GlobalTelemetryBus.BytesToMegabytes);
        }

        private void CancelMemoryLifecycleTransition()
        {
            if (!_memoryLifecycleTransitionActive)
                return;

            H8Memory.CancelSceneTransitionPurge();
            _memoryLifecycleTransitionActive = false;
            _memoryLifecycleSceneUnloadObserved = false;
            _lastSceneOwnedVaultRemainingCount = 0;
            _lastSceneOwnedVaultLockedCount = 0;
            _lastSceneOwnedVaultRemainingBytes = 0L;
            PublishMemoryLifecyclePause(paused: false, MemoryTransitionFailedFlag);
        }

        private void PublishMemoryLifecyclePause(bool paused, byte flags)
        {
            SystemPauseSignal signal = default;
            signal.SourceHash = MemoryTransitionPauseSourceHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Sequence = unchecked(++_memoryLifecyclePauseSequence);
            signal.Paused = paused ? (byte)1 : (byte)0;
            signal.Flags = flags;
            signal.RestoreScalar = 1f;
            SignalBus<SystemPauseSignal>.TryPush(in signal);
        }

        private bool ReleaseSceneOwnedVaultBuffers()
        {
            _lastSceneOwnedVaultRemainingCount = 0;
            _lastSceneOwnedVaultLockedCount = 0;
            _lastSceneOwnedVaultRemainingBytes = 0L;
            IDataVault vault = _dataVault;
            if (vault == null)
                vault = CacheDataVaultCold();
            if (vault == null)
                return true;

            long releasedBytes;
            int remainingCount;
            long remainingBytes;
            int lockedCount;
            vault.ReleaseSceneOwnedBuffers(out releasedBytes, out remainingCount, out remainingBytes, out lockedCount);
            _lastSceneOwnedVaultRemainingCount = remainingCount;
            _lastSceneOwnedVaultLockedCount = lockedCount;
            _lastSceneOwnedVaultRemainingBytes = remainingBytes;
            return remainingCount == 0 && lockedCount == 0;
        }

        private IDataVault CacheDataVaultCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private static bool ArePersistentWorldPoolsReadyForSceneActivation()
        {
            if (!GameBootstrapper.ArePreWarmAssetsReady)
                return false;

            ISceneTransitionWorldResidencyBridge registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            return registry.AreResidentWorldPrefabPoolsReady();
        }

        private static bool RequiresWorldResidencyGate(string nextSceneName)
        {
            return string.Equals(nextSceneName, WorldSceneName, StringComparison.Ordinal);
        }

        private static bool ShouldUseMainMenuCinematicTransition(Scene previousScene, string nextSceneName)
        {
            return previousScene.IsValid() &&
                   previousScene.isLoaded &&
                   string.Equals(previousScene.name, MainMenuSceneName, StringComparison.Ordinal) &&
                   string.Equals(nextSceneName, WorldSceneName, StringComparison.Ordinal);
        }

        private void BeginMainMenuCinematicTransition()
        {
            _cinematicTransitionActive = true;
            _cinematicTransitionElapsed = 0f;
            _transitionPerformanceWarningPublished = false;
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
                _cinematicCameraTargetPosition = _cinematicCameraStartPosition + (Vector3.down * MainMenuCameraPanDepth);
                _cinematicCameraTargetDelta = _cinematicCameraTargetPosition - _cinematicCameraStartPosition;
                _cinematicCameraTargetRotation =
                    _cinematicCameraStartRotation * Quaternion.Euler(MainMenuCameraPanPitchDegrees, 0f, 0f);
            }

            if (_cinematicMenuGroup != null)
            {
                _cinematicMenuStartAlpha = _cinematicMenuGroup.alpha;
                _cinematicMenuGroup.interactable = false;
                _cinematicMenuGroup.blocksRaycasts = false;
            }

            if (_cinematicMenuRect != null)
            {
                _cinematicMenuStartAnchoredPosition = _cinematicMenuRect.anchoredPosition;
                _cinematicMenuTargetAnchoredPosition =
                    _cinematicMenuStartAnchoredPosition + (Vector2.down * MainMenuUiSubmergePixels);
            }

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

            double solveStartTime = Time.realtimeSinceStartupAsDouble;
            _cinematicTransitionElapsed = math.min(
                TransitionDissolveSeconds,
                _cinematicTransitionElapsed + math.max(0f, unscaledDeltaTime));
            float normalized = MainMenuCameraPanDurationSeconds > 0f
                ? math.saturate(_cinematicTransitionElapsed / MainMenuCameraPanDurationSeconds)
                : 1f;
            float eased = SmoothStep01(normalized);

            ApplyCinematicCameraPose(eased, _cinematicTransitionElapsed);

            if (_transitionOverlayGroup != null)
                _transitionOverlayGroup.alpha = eased;
            if (_cinematicMenuGroup != null)
                _cinematicMenuGroup.alpha = _cinematicMenuStartAlpha * (1f - eased);
            if (_cinematicMenuRect != null)
                _cinematicMenuRect.anchoredPosition =
                    _cinematicMenuStartAnchoredPosition +
                    ((_cinematicMenuTargetAnchoredPosition - _cinematicMenuStartAnchoredPosition) * eased);

            SetTransitionDitherCoverage(1f);
            UpdateTerminalBootOverlay();
            PublishTransitionSolveBudgetWarningIfNeeded(solveStartTime);
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
                        await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
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
                double solveStartTime = Time.realtimeSinceStartupAsDouble;
                elapsed += math.max(0f, ResolveTransitionUnscaledDeltaTime());
                float normalized = TransitionDissolveSeconds > 0f
                    ? math.saturate(elapsed / TransitionDissolveSeconds)
                    : 1f;
                float eased = SmoothStep01(normalized);
                ApplyCinematicCameraPose(1f, MainMenuCameraPanDurationSeconds + elapsed);
                SetTransitionDitherCoverage(1f - eased);
                UpdateTerminalBootOverlay();
                UpdateWorldDroneCrossfade(eased);
                if (_transitionDitherMaterial == null)
                    _transitionOverlayGroup.alpha = 1f - eased;

                PublishTransitionSolveBudgetWarningIfNeeded(solveStartTime);
                await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
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
            float heave = CinematicMath.FastSin(elapsedSeconds * math.PI * 2f * CinematicHeaveFrequencyHz) *
                          CinematicHeaveAmplitude *
                          SmoothStep01(eased);
            Vector3 heaveOffset = (_cinematicCameraStartRotation * Vector3.up) * heave;
            cameraTransform.SetPositionAndRotation(
                _cinematicCameraStartPosition + (_cinematicCameraTargetDelta * eased) + heaveOffset,
                Quaternion.SlerpUnclamped(_cinematicCameraStartRotation, _cinematicCameraTargetRotation, eased));
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

            if (_transitionOverlayRoot != null)
                Destroy(_transitionOverlayRoot);

            _transitionOverlayRoot = null;
            _transitionOverlayGroup = null;
            _terminalBootText = null;
            _terminalBootLastFrame = -1;
            _transitionPerformanceWarningPublished = false;
            if (_transitionDitherMaterial != null)
                Destroy(_transitionDitherMaterial);
            _transitionDitherMaterial = null;
            ResetWorldEntryFreezeState();
        }

        private void PublishTransitionSolveBudgetWarningIfNeeded(double solveStartTime)
        {
            if (_transitionPerformanceWarningPublished || !Application.isPlaying)
                return;

            double elapsedMilliseconds = (Time.realtimeSinceStartupAsDouble - solveStartTime) * 1000.0d;
            if (elapsedMilliseconds < TransitionSolveTelemetryThresholdMs)
                return;

            _transitionPerformanceWarningPublished = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                TransitionSolveBudgetWarningHash,
                TransitionTelemetryContextHash,
                (float)elapsedMilliseconds);
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
            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher != null && dispatcher.SimulationPaused)
                dispatcher.RequestSimulationPause(false);

            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, 0f);
            Shader.SetGlobalFloat(_GamePausedId, 0f);
        }

        private static void BeginWorldDroneCrossfade()
        {
            if (Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance is ISceneTransitionAudioBridge spatialAudio)
            {
                spatialAudio.BeginWorldDroneTransition(
                    WorldDroneLoadDb,
                    WorldDroneRuntimeDb,
                    TransitionDissolveSeconds);
            }
        }

        private static void UpdateWorldDroneCrossfade(float normalized)
        {
            if (Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance is ISceneTransitionAudioBridge spatialAudio)
                spatialAudio.SetWorldDroneTransitionProgress(normalized);
        }

        private static void BeginInputReclaimInterpolation()
        {
            ICameraJuiceSystem cameraJuice = GlobalRegistry.CameraJuice;
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
                _transitionDitherMaterial = new Material(ditherShader); // COLD ALLOC: Material[1] - IGN scene dissolve material - owner: SceneRuntimeService
                _transitionDitherMaterial.SetColor(_TransitionDitherColorId, _TransitionAbyssColor);
                _transitionDitherMaterial.SetFloat(_TransitionDitherProgressId, 1f);
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
            AppendAsciiLiteral(buffer, ref cursor, _TerminalBootNeuralInterfaceBytes);
            AppendNewLine(buffer, ref cursor);
            AppendAsciiLiteral(buffer, ref cursor, _TerminalBootAupSectorBytes);
            AppendHex8(buffer, ref cursor, MixTerminalBootHash(_terminalBootSeed, (uint)frame));
            AppendAsciiLiteral(buffer, ref cursor, _TerminalBootSectorSeparatorBytes);
            AppendHex8(buffer, ref cursor, MixTerminalBootHash(_terminalBootSeed ^ 0xA53A9D2Du, (uint)(frame + 17)));
            AppendNewLine(buffer, ref cursor);
            AppendAsciiLiteral(buffer, ref cursor, _TerminalBootMaskBytes);
            AppendHex8(buffer, ref cursor, MixTerminalBootHash(_terminalBootSeed ^ 0xC2B2AE35u, (uint)(frame + 31)));
            AppendNewLine(buffer, ref cursor);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootDispatcherLabelBytes, GlobalRegistry.Dispatcher, 0u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootTickLabelBytes, GlobalRegistry.TickManager, 1u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootSceneLabelBytes, GlobalRegistry.Scene, 2u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootPhysicsLabelBytes, GlobalRegistry.Physics, 3u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootAudioLabelBytes, Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance, 4u, _terminalBootSeed, (uint)frame);

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
            uint state = seed ^ (TerminalBootHashSalt + (value * 0x9E3779B9u));
            if (state == 0u)
                state = TerminalBootHashSalt;

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static void AppendAsciiLiteral(Span<char> buffer, ref int cursor, byte[] literal)
        {
            if (literal == null)
                return;

            for (int i = 0; i < literal.Length && cursor < buffer.Length; i++)
                buffer[cursor++] = (char)literal[i];
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

        private static void AppendServiceHandle(
            Span<char> buffer,
            ref int cursor,
            byte[] label,
            object service,
            uint serviceIndex,
            uint bootSeed,
            uint frame)
        {
            AppendAsciiLiteral(buffer, ref cursor, _TerminalBootServicePrefixBytes);
            AppendAsciiLiteral(buffer, ref cursor, label);
            AppendAsciiLiteral(buffer, ref cursor, _TerminalBootServiceHandlePrefixBytes);
            if (service == null)
            {
                AppendAsciiLiteral(buffer, ref cursor, IntPtr.Size == 8 ? _TerminalBootZeroHandle64Bytes : _TerminalBootZeroHandle32Bytes);
                AppendNewLine(buffer, ref cursor);
                return;
            }

            uint low = MixTerminalBootHash(bootSeed ^ (serviceIndex * 0x85EBCA6Bu), frame + serviceIndex);
            if (IntPtr.Size == 8)
            {
                uint high = MixTerminalBootHash(bootSeed ^ (serviceIndex * 0xC2B2AE35u), frame + serviceIndex + 0x9E3779B9u);
                AppendHex16(buffer, ref cursor, ((ulong)high << 32) | low);
            }
            else
            {
                AppendHex8(buffer, ref cursor, low);
            }

            AppendNewLine(buffer, ref cursor);
        }

        private bool IsSceneActivationGateReady(
            bool useCinematicTransition,
            bool loadReady,
            bool poolsReady,
            bool originStable,
            bool gpuResidencyReady)
        {
            if (!loadReady || !poolsReady || !originStable || !gpuResidencyReady)
                return false;

            if (!useCinematicTransition)
                return true;

            return HasMainMenuDissolveReachedActivationTime(_cinematicTransitionElapsed);
        }

        private static bool HasMainMenuDissolveReachedActivationTime(float elapsedSeconds)
        {
            return elapsedSeconds == TransitionDissolveSeconds;
        }

        private void SetTransitionDitherCoverage(float coverage)
        {
            if (_transitionDitherMaterial == null)
                return;

            _transitionDitherMaterial.SetFloat(_TransitionDitherProgressId, math.saturate(coverage));
        }

        private static float SmoothStep01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        private static float ResolveTransitionUnscaledDeltaTime()
        {
            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.isfinite(deltaTime) && deltaTime > 0f
                ? deltaTime
                : 0.0166667f;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string blockedBy = GetSceneActivationBlockedReason(loadReady, poolsReady, originStable, gpuResidencyReady);
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' still blocked after {waitFrames} frames. Reason: {blockedBy}. Progress: {progress:0.00}.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedInFlight(string sceneName, string pendingSceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SceneRuntimeService] Scene load '{sceneName}' rejected because '{pendingSceneName}' is already in flight.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedBootstrapIncomplete(string sceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' rejected while bootstrap is incomplete.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadOperationMissing(string sceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' failed to create an AsyncOperation.");
#endif
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

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void RestoreCoreTickAfterRuntimeStateClear()
        {
            if (!_isInitialized || !Application.isPlaying || !isActiveAndEnabled)
                return;

            _registeredUpdatable = false;
            TryRegisterUpdatable();
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null || !_isInitialized || !isActiveAndEnabled)
                        return;

                    TryUnregisterUpdatable();
                    TryRegisterUpdatable();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
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
