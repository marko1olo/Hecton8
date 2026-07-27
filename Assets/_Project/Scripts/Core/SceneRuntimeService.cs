using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hecton8.Bootstrap;
using Hecton8.Construction;
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
    public sealed class SceneRuntimeService : MonoBehaviour, ISceneService, IUpdatable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001SceneRuntimeServiceSignalPushDropCount;
        private const int SceneActivationWatchdogInitialFrames = 1200;
        private const int SceneActivationWatchdogRepeatFrames = 300;
        private const double SceneActivationEmergencyReleaseSeconds = 35d;
        private const double ManagedSceneUnloadWatchdogSeconds = 20d;
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string OrbitSceneName = "01_ORBIT";
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const string TransitionOverlayRootName = "[SceneRuntimeService_TransitionOverlay]";
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
        private const float TransitionOverlayReferenceWidth = 1920f;
        private const float TransitionOverlayReferenceHeight = 1080f;
        private const float TransitionOverlayCameraDistance = 0.45f;
        private const float MinimumTransitionDitherCoverageScale = 0.35f;
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
        private static readonly double _stopwatchTicksToMilliseconds = 1000.0d / Stopwatch.Frequency;
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

        [Header("Authored Transition Assets")]
        [SerializeField, Tooltip("Dedicated authored material instance for the scene transition dither overlay. Must not be shared with gameplay/UI surfaces.")]
        private Material transitionDitherMaterial;

        private static bool _suppressRuntimeClearForManagedUnload;
        private bool _isInitialized;
        private bool _registeredSceneService;
        private bool _registeredSceneCallbacks;
        private bool _registeredUpdatable;
        private bool _registeredLateFrameTickable;
        private bool _registeredHotSwapListener;
        private bool _runtimeOwnerAborted;
        private bool _dispatcherAvailable;
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
        private Vector3 _cinematicCameraControlA;
        private Vector3 _cinematicCameraControlB;
        private Vector3 _cinematicCameraTargetDelta;
        private Quaternion _cinematicCameraStartRotation;
        private Quaternion _cinematicCameraTargetRotation;
        private Vector2 _cinematicMenuStartAnchoredPosition;
        private Vector2 _cinematicMenuTargetAnchoredPosition;
        private float _cinematicMenuStartAlpha;
        private GameObject _transitionOverlayRoot;
        private RectTransform _transitionOverlayRect;
        private Canvas _transitionOverlayCanvas;
        private CanvasGroup _transitionOverlayGroup;
        private Camera _transitionOverlayCamera;
        private Material _resolvedTransitionDitherMaterial;
        private bool _ownsResolvedTransitionDitherMaterial;
        private TMP_Text _terminalBootText;
        // COLD ALLOC: char[384] - transition terminal boot text buffer - owner: SceneRuntimeService
        private readonly char[] _terminalBootBuffer = new char[TerminalBootBufferLength];
        // COLD ALLOC: List<GameObject>[64] - scene-load camera root search scratch - owner: SceneRuntimeService
        private readonly List<GameObject> _cameraRootSearchBuffer = new List<GameObject>(64);
        // COLD ALLOC: List<Camera>[16] - scene-load camera search scratch - owner: SceneRuntimeService
        private readonly List<Camera> _cameraSearchBuffer = new List<Camera>(16);
        private object _terminalBootDispatcherService;
        private object _terminalBootTickService;
        private object _terminalBootSceneService;
        private object _terminalBootPhysicsService;
        private object _terminalBootAudioService;
        private ITickDispatcher _tickDispatcher;
        private object _sceneTransitionAudioRuntime;
        private ISceneTransitionAudioBridge _sceneTransitionAudioBridge;
        private ICameraJuiceSystem _cameraJuiceSystem;
        private uint _terminalBootSeed;
        private int _terminalBootLastFrame = -1;
        private float _transitionVisualOverkill01 = 1f;
        private float _transitionPresentationElapsedSeconds;
        private float _transitionPresentationEased;
        private float _transitionPresentationVisualOverkill01 = 1f;
        private float _transitionPresentationOverlayAlpha;
        private float _transitionPresentationDitherCoverage = 1f;
        private float _transitionPresentationDroneProgress;
        private bool _transitionPresentationDriveMenu;
        private bool _transitionPresentationDirty;
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
            SceneRuntimeService runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            ISceneService registeredScene = GlobalRegistry.Scene;
            if (IsSceneServiceUsable(registeredScene) &&
                ReferenceEquals(registeredScene as SceneRuntimeService, null))
            {
                return null;
            }

            GameObject runtimeRoot = new GameObject("[SceneRuntimeService]"); // COLD ALLOC: GameObject[1] - persistent scene service owner - owner: SceneRuntimeService

            // Park under the project persistent root BEFORE AddComponent, so Awake observes the final
            // hierarchy. Left unparented this object lands in whatever scene is active at creation time -
            // 00_BOOTSTRAP - and is destroyed when that scene unloads on the way to 02_HECTON_WORLD,
            // taking ISceneService down with it exactly when the next scene load needs it.
            //
            // Raw DontDestroyOnLoad is not the alternative: AGENTS.md:336 forbids it in first-party
            // runtime, and GameBootstrapper.EnforceProjectPersistentRoot destroys every
            // DontDestroyOnLoad root that is not the bootstrapper or a child of it. Being a child of that
            // single persistent root is the sanctioned way to survive a scene transition here.
            //
            // A null owner is legitimate - isolated sandbox and render-test scenes run without a
            // bootstrapper - so fall back to the previous unparented behaviour rather than refusing to
            // create the service.
            GameBootstrapper persistentOwner = GlobalRegistry.BootstrapperRuntime;
            if (persistentOwner != null)
                runtimeRoot.transform.SetParent(persistentOwner.transform, false);

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
            if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())
                return;

            if (!TryRegisterSceneService())
                return;

            H8Memory.Initialize();
            _dataVault = GlobalRegistry.DataVault;
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            RefreshTerminalBootServiceHandlesCold();
            TryRegisterHotSwapListener();

            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterLateFrameTickable();
                TryRegisterSceneCallbacks();
                return;
            }

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterLateFrameTickable();
            TryRegisterSceneCallbacks();
        }

        /// <summary>
        /// Performs a guarded scene transition after clearing registry state.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        public void LoadScene(string sceneName)
        {
            string requestedSceneName = NormalizeRequestedSceneName(sceneName);
            if (requestedSceneName.Length == 0)
            {
                LogSceneLoadRejectedInvalidName(sceneName);
                return;
            }

            sceneName = requestedSceneName;
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
            string requestedSceneName = NormalizeRequestedSceneName(sceneName);
            if (requestedSceneName.Length == 0)
            {
                LogSceneLoadRejectedInvalidName(sceneName);
                return;
            }

            sceneName = requestedSceneName;
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
                GlobalRegistry.BeginSceneRuntimePublicationGate();
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
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                bool emergencyReleaseIssued = false;

                while (Application.isPlaying && _isInitialized && isActiveAndEnabled && !_pendingSceneLoadOperation.isDone)
                {
                    if (useCinematicTransition)
                        AdvanceMainMenuCinematicTransitionState(ResolveTransitionUnscaledDeltaTime());

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
                    else if (!_sceneActivationReleased &&
                             !emergencyReleaseIssued &&
                             HasSceneActivationEmergencyElapsed(waitStartTimestamp, out double elapsedSeconds))
                    {
                        ReleaseSceneActivation(_pendingSceneLoadOperation);
                        _sceneActivationReleased = true;
                        emergencyReleaseIssued = true;
                        LogSceneActivationEmergencyRelease(
                            sceneName,
                            _pendingSceneLoadOperation.progress,
                            loadReady,
                            poolsReady,
                            originStable,
                            gpuResidencyReady,
                            waitFrames,
                            elapsedSeconds);
                    }
                    else if (waitFrames >= nextWatchdogFrame)
                    {
                        LogSceneActivationWatchdog(sceneName, _pendingSceneLoadOperation.progress, loadReady, poolsReady, originStable, gpuResidencyReady, waitFrames);
                        nextWatchdogFrame = waitFrames + SceneActivationWatchdogRepeatFrames;
                    }

                    waitFrames++;
                    await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
                }

                if (useCinematicTransition && Application.isPlaying && _isInitialized && isActiveAndEnabled)
                    await CompleteMainMenuCinematicTransitionAsync(previousScene, sceneName);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                GlobalRegistry.EndSceneRuntimePublicationGate();
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

        public void LateFrameTick()
        {
            ApplyQueuedMainMenuCinematicPresentation();
        }

        private void Awake()
        {
            EnsureRuntimeOwnership();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())
                return;

            TryRegisterUpdatable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            if (_isInitialized)
            {
                if (!TryRegisterSceneService())
                    return;

                TryRegisterSceneCallbacks();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
            TryUnregisterLateFrameTickable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_memoryLifecycleTransitionActive)
                CancelMemoryLifecycleTransition();

            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
            TryUnregisterLateFrameTickable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
            EndMainMenuCinematicTransition();
            _dispatcherAvailable = false;
            _sceneLoadInFlight = false;
            _pendingSceneName = null;
            _pendingSceneLoadOperation = null;
            _gpuResidencyReadyFrame = -1;
            _sceneActivationReleased = false;
            ClearTerminalBootServiceHandles();
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
            DroneFleetManager.ClearSceneTransitionRuntimeState();
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
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Sequence = unchecked(++_memoryLifecyclePauseSequence);
            signal.Paused = paused ? (byte)1 : (byte)0;
            signal.Flags = flags;
            signal.RestoreScalar = 1f;
            SignalBus<SystemPauseSignal>.TryPushTracked(in signal, ref s_x001SceneRuntimeServiceSignalPushDropCount);
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
            ISceneTransitionWorldResidencyBridge registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return true;

            if (!GameBootstrapper.ArePreWarmAssetsReady)
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
                   (string.Equals(nextSceneName, WorldSceneName, StringComparison.Ordinal) ||
                    string.Equals(nextSceneName, OrbitSceneName, StringComparison.Ordinal));
        }

        private void BeginMainMenuCinematicTransition()
        {
            _cinematicTransitionActive = true;
            _cinematicTransitionElapsed = 0f;
            _transitionPerformanceWarningPublished = false;
            _cinematicCamera = ResolveMainMenuCinematicCameraCold();
            BindTransitionOverlayCameraCold(_cinematicCamera);
            ClearTransitionPresentationState();
            _transitionVisualOverkill01 = 1f;
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
                Vector3 forward = _cinematicCameraStartRotation * Vector3.forward;
                _cinematicCameraControlA =
                    _cinematicCameraStartPosition + (forward * 1.65f) + (Vector3.down * 1.15f);
                _cinematicCameraControlB =
                    _cinematicCameraTargetPosition - (forward * 2.15f) + (Vector3.down * 0.35f);
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
            _terminalBootSeed = ComputeTerminalBootSeed(_pendingSceneName, SystemDispatcher.CurrentFrameIndex);
            _terminalBootLastFrame = -1;
            QueueMainMenuCinematicPresentation(
                0f,
                0f,
                _transitionVisualOverkill01,
                0f,
                1f,
                0f,
                driveMenu: true);
        }

        private void AdvanceMainMenuCinematicTransitionState(float unscaledDeltaTime)
        {
            if (!_cinematicTransitionActive)
                return;

            _cinematicTransitionElapsed = math.min(
                TransitionDissolveSeconds,
                _cinematicTransitionElapsed + math.max(0f, unscaledDeltaTime));
            float normalized = MainMenuCameraPanDurationSeconds > 0f
                ? math.saturate(_cinematicTransitionElapsed / MainMenuCameraPanDurationSeconds)
                : 1f;
            float eased = SmoothStep01(normalized);
            float visualOverkill01 = UpdateTransitionVisualOverkill01(normalized);

            QueueMainMenuCinematicPresentation(
                _cinematicTransitionElapsed,
                eased,
                visualOverkill01,
                eased,
                1f,
                0f,
                driveMenu: true);
        }

        private void QueueMainMenuCinematicPresentation(
            float elapsedSeconds,
            float eased,
            float visualOverkill01,
            float overlayAlpha,
            float ditherCoverage,
            float droneProgress,
            bool driveMenu)
        {
            _transitionPresentationElapsedSeconds = math.max(0f, math.select(0f, elapsedSeconds, math.isfinite(elapsedSeconds)));
            _transitionPresentationEased = math.saturate(math.select(0f, eased, math.isfinite(eased)));
            _transitionPresentationVisualOverkill01 = math.saturate(math.select(1f, visualOverkill01, math.isfinite(visualOverkill01)));
            _transitionPresentationOverlayAlpha = math.saturate(math.select(0f, overlayAlpha, math.isfinite(overlayAlpha)));
            _transitionPresentationDitherCoverage = math.saturate(math.select(0f, ditherCoverage, math.isfinite(ditherCoverage)));
            _transitionPresentationDroneProgress = math.saturate(math.select(0f, droneProgress, math.isfinite(droneProgress)));
            _transitionPresentationDriveMenu = driveMenu;
            _transitionPresentationDirty = true;
        }

        private void ApplyQueuedMainMenuCinematicPresentation()
        {
            if (!_transitionPresentationDirty)
                return;

            if (!_cinematicTransitionActive)
            {
                _transitionPresentationDirty = false;
                return;
            }

            _transitionPresentationDirty = false;
            long solveStartTicks = Stopwatch.GetTimestamp();
            float eased = _transitionPresentationEased;
            ApplyCinematicCameraPose(
                eased,
                _transitionPresentationElapsedSeconds,
                _transitionPresentationVisualOverkill01);
            PlaceTransitionOverlayInCameraView();

            if (_transitionOverlayGroup != null)
                _transitionOverlayGroup.alpha = _transitionPresentationOverlayAlpha;
            if (_transitionPresentationDriveMenu && _cinematicMenuGroup != null)
                _cinematicMenuGroup.alpha = _cinematicMenuStartAlpha * (1f - eased);
            if (_transitionPresentationDriveMenu && _cinematicMenuRect != null)
                _cinematicMenuRect.anchoredPosition =
                    _cinematicMenuStartAnchoredPosition +
                    ((_cinematicMenuTargetAnchoredPosition - _cinematicMenuStartAnchoredPosition) * eased);

            SetTransitionDitherCoverage(_transitionPresentationDitherCoverage, _transitionPresentationVisualOverkill01);
            UpdateTerminalBootOverlay();
            UpdateWorldDroneCrossfade(_transitionPresentationDroneProgress);
            PublishTransitionSolveBudgetWarningIfNeeded(solveStartTicks);
        }

        private async Awaitable CompleteMainMenuCinematicTransitionAsync(Scene previousScene, string loadedSceneName)
        {
            Scene loadedScene = SceneManager.GetSceneByName(loadedSceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                Camera loadedSceneCamera = ResolvePrimarySceneCameraCold(loadedScene);
                if (loadedSceneCamera != null)
                    BindTransitionOverlayCameraCold(loadedSceneCamera);
            }

            if (previousScene.IsValid() && previousScene.isLoaded && !string.Equals(previousScene.name, loadedSceneName, StringComparison.Ordinal))
            {
                _suppressRuntimeClearForManagedUnload = true;
                try
                {
                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene);
                    int unloadWaitFrames = 0;
                    long unloadStartTimestamp = Stopwatch.GetTimestamp();
                    while (unloadOperation != null && !unloadOperation.isDone)
                    {
                        if (HasManagedSceneUnloadWatchdogElapsed(unloadStartTimestamp, out double elapsedSeconds))
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Hecton8.Core.H8Debug.LogError(
                                $"[SceneRuntimeService] Managed unload for scene '{previousScene.name}' exceeded {elapsedSeconds:0.00}s/{unloadWaitFrames} frames; continuing transition fail-open.");
#endif
                            break;
                        }

                        unloadWaitFrames++;
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

        private static bool HasManagedSceneUnloadWatchdogElapsed(long startTimestamp, out double elapsedSeconds)
        {
            elapsedSeconds = (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
            return elapsedSeconds >= ManagedSceneUnloadWatchdogSeconds;
        }

        private async Awaitable DissolveTransitionOverlayAsync()
        {
            EnsureTransitionOverlay();
            if (_transitionOverlayGroup == null)
            {
                QueueMainMenuCinematicPresentation(
                    MainMenuCameraPanDurationSeconds,
                    1f,
                    _transitionVisualOverkill01,
                    0f,
                    0f,
                    1f,
                    driveMenu: false);
                await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
                return;
            }

            QueueMainMenuCinematicPresentation(
                MainMenuCameraPanDurationSeconds,
                1f,
                _transitionVisualOverkill01,
                1f,
                1f,
                0f,
                driveMenu: false);
            float elapsed = 0f;
            while (Application.isPlaying && elapsed < TransitionDissolveSeconds)
            {
                elapsed += math.max(0f, ResolveTransitionUnscaledDeltaTime());
                float normalized = TransitionDissolveSeconds > 0f
                    ? math.saturate(elapsed / TransitionDissolveSeconds)
                    : 1f;
                float eased = SmoothStep01(normalized);
                float visualOverkill01 = UpdateTransitionVisualOverkill01(normalized);
                float overlayAlpha = _resolvedTransitionDitherMaterial == null
                    ? 1f - eased
                    : 1f;
                QueueMainMenuCinematicPresentation(
                    MainMenuCameraPanDurationSeconds + elapsed,
                    1f,
                    visualOverkill01,
                    overlayAlpha,
                    1f - eased,
                    eased,
                    driveMenu: false);
                await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
            }

            QueueMainMenuCinematicPresentation(
                MainMenuCameraPanDurationSeconds + TransitionDissolveSeconds,
                1f,
                _transitionVisualOverkill01,
                0f,
                0f,
                1f,
                driveMenu: false);
            await AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
        }

        private void ApplyCinematicCameraPose(float eased, float elapsedSeconds, float visualOverkill01)
        {
            if (_cinematicCamera == null)
                return;

            Transform cameraTransform = _cinematicCamera.transform;
            float safeVisualOverkill = math.saturate(math.select(1f, visualOverkill01, math.isfinite(visualOverkill01)));
            float heave = CinematicMath.FastSin(elapsedSeconds * math.PI * 2f * CinematicHeaveFrequencyHz) *
                           CinematicHeaveAmplitude *
                           SmoothStep01(eased) *
                           safeVisualOverkill;
            Vector3 heaveOffset = (_cinematicCameraStartRotation * Vector3.up) * heave;
            Vector3 splinePosition = ResolveCubicBezier(
                _cinematicCameraStartPosition,
                _cinematicCameraControlA,
                _cinematicCameraControlB,
                _cinematicCameraTargetPosition,
                eased);
            cameraTransform.SetPositionAndRotation(
                splinePosition + heaveOffset,
                Quaternion.SlerpUnclamped(_cinematicCameraStartRotation, _cinematicCameraTargetRotation, eased));
        }

        private static Vector3 ResolveCubicBezier(Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end, float t)
        {
            float x = math.saturate(math.isfinite(t) ? t : 0f);
            float omt = 1f - x;
            float omt2 = omt * omt;
            float t2 = x * x;
            return (start * (omt2 * omt)) +
                   (controlA * (3f * omt2 * x)) +
                   (controlB * (3f * omt * t2)) +
                   (end * (t2 * x));
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
                DestroyTransitionOverlayRoot(_transitionOverlayRoot);

            ClearTransitionOverlayObjectReferences();
            _transitionOverlayCamera = null;
            _terminalBootLastFrame = -1;
            _transitionPerformanceWarningPublished = false;
            DestroyTransitionDitherMaterial();
            ClearTransitionPresentationState();
            ResetWorldEntryFreezeState();
        }

        private void ClearTransitionPresentationState()
        {
            _transitionPresentationElapsedSeconds = 0f;
            _transitionPresentationEased = 0f;
            _transitionPresentationVisualOverkill01 = 1f;
            _transitionPresentationOverlayAlpha = 0f;
            _transitionPresentationDitherCoverage = 1f;
            _transitionPresentationDroneProgress = 0f;
            _transitionPresentationDriveMenu = false;
            _transitionPresentationDirty = false;
        }

        private void ClearTransitionOverlayObjectReferences()
        {
            _transitionOverlayRoot = null;
            _transitionOverlayRect = null;
            _transitionOverlayCanvas = null;
            _transitionOverlayGroup = null;
            _terminalBootText = null;
        }

        private void DestroyTransitionDitherMaterial()
        {
            if (_ownsResolvedTransitionDitherMaterial && _resolvedTransitionDitherMaterial != null)
                Destroy(_resolvedTransitionDitherMaterial);

            _resolvedTransitionDitherMaterial = null;
            _ownsResolvedTransitionDitherMaterial = false;
        }

        private void PublishTransitionSolveBudgetWarningIfNeeded(long solveStartTicks)
        {
            if (_transitionPerformanceWarningPublished || !Application.isPlaying)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - solveStartTicks;
            if (elapsedTicks <= 0L)
                return;

            double elapsedMilliseconds = elapsedTicks * _stopwatchTicksToMilliseconds;
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

        private void ResetWorldEntryFreezeState()
        {
            ResetWorldEntryFreezeStateFromCache();

            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, 0f);
            Shader.SetGlobalFloat(_GamePausedId, 0f);
        }

        private void ResetWorldEntryFreezeStateFromCache()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null && dispatcher.SimulationPaused)
                dispatcher.RequestSimulationPause(false);
        }

        private void BeginWorldDroneCrossfade()
        {
            ISceneTransitionAudioBridge spatialAudio = ResolveSceneTransitionAudioBridge();
            if (spatialAudio == null)
                return;

            spatialAudio.BeginWorldDroneTransition(
                WorldDroneLoadDb,
                WorldDroneRuntimeDb,
                TransitionDissolveSeconds);
        }

        private void UpdateWorldDroneCrossfade(float normalized)
        {
            ISceneTransitionAudioBridge spatialAudio = ResolveSceneTransitionAudioBridge();
            if (spatialAudio != null)
                spatialAudio.SetWorldDroneTransitionProgress(normalized);
        }

        private void BeginInputReclaimInterpolation()
        {
            ICameraJuiceSystem cameraJuice = _cameraJuiceSystem;
            if (cameraJuice != null)
                cameraJuice.BeginInputReclaimFov(InputReclaimStartFov, InputReclaimDurationSeconds);
        }

        private void EnsureTransitionOverlay()
        {
            if (_transitionOverlayRoot != null)
                return;

            GameObject root = new GameObject(TransitionOverlayRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup)); // COLD ALLOC: GameObject[1] - scene transition blackout overlay - owner: SceneRuntimeService
            root.transform.SetParent(transform, false);
            if (!root.TryGetComponent(out RectTransform rootRect) ||
                !root.TryGetComponent(out Canvas canvas) ||
                !root.TryGetComponent(out CanvasGroup overlayGroup))
            {
                AbortTransitionOverlayCreation(root);
                return;
            }

            rootRect.sizeDelta = new Vector2(TransitionOverlayReferenceWidth, TransitionOverlayReferenceHeight);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _transitionOverlayCamera;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = TransitionOverlaySortingOrder;

            _transitionOverlayRect = rootRect;
            _transitionOverlayCanvas = canvas;
            _transitionOverlayGroup = overlayGroup;
            _transitionOverlayGroup.alpha = 0f;
            _transitionOverlayGroup.interactable = false;
            _transitionOverlayGroup.blocksRaycasts = true;
            PlaceTransitionOverlayInCameraView();

            GameObject imageRoot = new GameObject("DitherBlackout", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] - full-screen dither image - owner: SceneRuntimeService
            imageRoot.transform.SetParent(root.transform, false);
            if (!imageRoot.TryGetComponent(out RectTransform imageRect) ||
                !imageRoot.TryGetComponent(out Image image))
            {
                AbortTransitionOverlayCreation(root);
                return;
            }

            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            image.raycastTarget = true;
            image.color = _TransitionAbyssColor;

            Material ditherMaterial = ResolveTransitionDitherMaterial();
            if (ditherMaterial != null)
            {
                _resolvedTransitionDitherMaterial = ditherMaterial;
                _resolvedTransitionDitherMaterial.SetColor(_TransitionDitherColorId, _TransitionAbyssColor);
                _resolvedTransitionDitherMaterial.SetFloat(_TransitionDitherProgressId, 1f);
                image.material = _resolvedTransitionDitherMaterial;
            }

            GameObject terminalRoot = new GameObject("TerminalBootOverlay", typeof(RectTransform), typeof(TextMeshProUGUI)); // COLD ALLOC: GameObject[1] - zero-GC terminal boot overlay text - owner: SceneRuntimeService
            terminalRoot.transform.SetParent(root.transform, false);
            if (!terminalRoot.TryGetComponent(out RectTransform terminalRect) ||
                !terminalRoot.TryGetComponent(out TextMeshProUGUI terminalText))
            {
                AbortTransitionOverlayCreation(root);
                return;
            }

            terminalRect.anchorMin = Vector2.zero;
            terminalRect.anchorMax = Vector2.one;
            terminalRect.offsetMin = new Vector2(48f, 40f);
            terminalRect.offsetMax = new Vector2(-48f, -40f);

            terminalText.raycastTarget = false;
            terminalText.alignment = TextAlignmentOptions.BottomLeft;
            terminalText.textWrappingMode = TextWrappingModes.NoWrap;
            terminalText.fontSize = 17f;
            terminalText.color = _TerminalBootTextColor;
            _terminalBootText = terminalText;

            _transitionOverlayRoot = root;
        }

        private void AbortTransitionOverlayCreation(GameObject root)
        {
            DestroyTransitionOverlayRoot(root);
            ClearTransitionOverlayObjectReferences();
            DestroyTransitionDitherMaterial();
        }

        private Material ResolveTransitionDitherMaterial()
        {
            _ownsResolvedTransitionDitherMaterial = false;
            if (transitionDitherMaterial != null)
                return transitionDitherMaterial;

            Hecton8.Core.H8Debug.LogError("[SceneRuntimeService] Missing authored transitionDitherMaterial. Scene transitions fall back to solid blackout only; runtime material creation is forbidden.");
            return null;
        }

        private void PlaceTransitionOverlayInCameraView()
        {
            RectTransform overlayRect = _transitionOverlayRect;
            Camera overlayCamera = _transitionOverlayCamera != null
                ? _transitionOverlayCamera
                : _cinematicCamera;
            if (overlayRect == null || overlayCamera == null)
                return;

            Transform cameraTransform = overlayCamera.transform;
            float distance = math.max(0.01f, TransitionOverlayCameraDistance);
            float aspect = math.isfinite(overlayCamera.aspect)
                ? math.max(0.01f, overlayCamera.aspect)
                : TransitionOverlayReferenceWidth / TransitionOverlayReferenceHeight;
            float viewHeight;
            if (overlayCamera.orthographic)
            {
                float orthographicSize = math.isfinite(overlayCamera.orthographicSize)
                    ? overlayCamera.orthographicSize
                    : 0.5f;
                viewHeight = math.max(0.01f, orthographicSize * 2f);
            }
            else
            {
                float fov = math.isfinite(overlayCamera.fieldOfView)
                    ? math.clamp(overlayCamera.fieldOfView, 1f, 179f)
                    : 60f;
                viewHeight = math.max(0.01f, 2f * distance * math.tan(math.radians(fov) * 0.5f));
            }

            float viewWidth = viewHeight * aspect;
            float scale = math.max(
                viewWidth / TransitionOverlayReferenceWidth,
                viewHeight / TransitionOverlayReferenceHeight);
            overlayRect.SetPositionAndRotation(
                cameraTransform.position + (cameraTransform.forward * distance),
                cameraTransform.rotation);
            overlayRect.localScale = new Vector3(scale, scale, scale);

            Canvas overlayCanvas = _transitionOverlayCanvas;
            if (overlayCanvas != null)
                overlayCanvas.worldCamera = overlayCamera;
        }

        private Camera ResolveMainMenuCinematicCameraCold()
        {
            Camera configuredCamera = _configuredCinematicCamera;
            if (configuredCamera != null && configuredCamera.enabled)
                return configuredCamera;

            Camera activeSceneCamera = ResolvePrimarySceneCameraCold(SceneManager.GetActiveScene());
            if (activeSceneCamera != null)
                return activeSceneCamera;

            return null;
        }

        private void BindTransitionOverlayCameraCold(Camera camera)
        {
            _transitionOverlayCamera = camera;

            Canvas overlayCanvas = _transitionOverlayCanvas;
            if (overlayCanvas != null)
                overlayCanvas.worldCamera = camera;
        }

        private Camera ResolvePrimarySceneCameraCold(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            _cameraRootSearchBuffer.Clear();
            scene.GetRootGameObjects(_cameraRootSearchBuffer);
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
                    if (camera != null && camera.enabled)
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

        private static void DestroyTransitionOverlayRoot(GameObject root)
        {
            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        private void UpdateTerminalBootOverlay()
        {
            if (_terminalBootText == null)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
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
            AppendServiceHandle(buffer, ref cursor, _TerminalBootDispatcherLabelBytes, _terminalBootDispatcherService, 0u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootTickLabelBytes, _terminalBootTickService, 1u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootSceneLabelBytes, _terminalBootSceneService, 2u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootPhysicsLabelBytes, _terminalBootPhysicsService, 3u, _terminalBootSeed, (uint)frame);
            AppendServiceHandle(buffer, ref cursor, _TerminalBootAudioLabelBytes, _terminalBootAudioService, 4u, _terminalBootSeed, (uint)frame);

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

        private static string NormalizeRequestedSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
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
            return elapsedSeconds >= TransitionDissolveSeconds - math.EPSILON;
        }

        private static bool HasSceneActivationEmergencyElapsed(long startTimestamp, out double elapsedSeconds)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
            return elapsedSeconds >= SceneActivationEmergencyReleaseSeconds;
        }

        private float UpdateTransitionVisualOverkill01(float normalized)
        {
            float targetQuality = ResolveGlobalQualityWeight01();
            float desiredVisualOverkill01 = math.lerp(1f, targetQuality, SmoothStep01(normalized));
            _transitionVisualOverkill01 = math.min(_transitionVisualOverkill01, desiredVisualOverkill01);
            return _transitionVisualOverkill01;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private void SetTransitionDitherCoverage(float coverage, float visualOverkill01)
        {
            if (_resolvedTransitionDitherMaterial == null)
                return;

            float safeVisualOverkill = math.saturate(math.select(1f, visualOverkill01, math.isfinite(visualOverkill01)));
            float qualityCoverageScale = math.lerp(MinimumTransitionDitherCoverageScale, 1f, safeVisualOverkill);
            _resolvedTransitionDitherMaterial.SetFloat(_TransitionDitherProgressId, math.saturate(coverage) * qualityCoverageScale);
        }

        private static float SmoothStep01(float value)
        {
            value = math.saturate(math.select(0f, value, math.isfinite(value)));
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
                _gpuResidencyReadyFrame = SystemDispatcher.CurrentFrameIndex + 1;
                return false;
            }

            return SystemDispatcher.CurrentFrameIndex >= _gpuResidencyReadyFrame;
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
            Hecton8.Core.H8Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' still blocked after {waitFrames} frames. Reason: {blockedBy}. Progress: {progress:0.00}.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneActivationEmergencyRelease(
            string sceneName,
            float progress,
            bool loadReady,
            bool poolsReady,
            bool originStable,
            bool gpuResidencyReady,
            int waitFrames,
            double elapsedSeconds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string blockedBy = GetSceneActivationBlockedReason(loadReady, poolsReady, originStable, gpuResidencyReady);
            Hecton8.Core.H8Debug.LogError(
                $"[SceneRuntimeService] Emergency-released scene load '{sceneName}' after {elapsedSeconds:0.00}s/{waitFrames} frames. Reason before release: {blockedBy}. Progress: {progress:0.00}.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedInvalidName(string sceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError($"[SceneRuntimeService] Scene load rejected because scene name is empty or whitespace. value='{sceneName}'.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedInFlight(string sceneName, string pendingSceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning($"[SceneRuntimeService] Scene load '{sceneName}' rejected because '{pendingSceneName}' is already in flight.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedBootstrapIncomplete(string sceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' rejected while bootstrap is incomplete.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadOperationMissing(string sceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' failed to create an AsyncOperation.");
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

            if (!_dispatcherAvailable)
                return;

            SystemDispatcher.Unregister((IUpdatable)this, PriorityLayer.Core);
            _registeredUpdatable = SystemDispatcher.Register((IUpdatable)this, PriorityLayer.Core);
        }

        private void RestoreCoreTickAfterRuntimeStateClear()
        {
            if (!_isInitialized || !Application.isPlaying || !isActiveAndEnabled)
                return;

            _registeredUpdatable = false;
            _registeredLateFrameTickable = false;
            TryRegisterUpdatable();
            TryRegisterLateFrameTickable();
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            SystemDispatcher.Unregister((IUpdatable)this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTickable || !Application.isPlaying)
                return;

            if (!_dispatcherAvailable)
                return;

            SystemDispatcher.Unregister((ILateFrameTickable)this, PriorityLayer.Core);
            _registeredLateFrameTickable = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.Core);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTickable)
                return;

            SystemDispatcher.Unregister((ILateFrameTickable)this, PriorityLayer.Core);
            _registeredLateFrameTickable = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _dispatcherAvailable = currentService != null;
                    _terminalBootDispatcherService = currentService;
                    _tickDispatcher = currentService as ITickDispatcher;
                    TryUnregisterUpdatable();
                    TryUnregisterLateFrameTickable();
                    if (!_dispatcherAvailable || !_isInitialized || !isActiveAndEnabled)
                        return;

                    TryRegisterUpdatable();
                    TryRegisterLateFrameTickable();
                    break;
                case GlobalRegistryServiceSlot.TickManager:
                    _terminalBootTickService = currentService;
                    break;
                case GlobalRegistryServiceSlot.Scene:
                    _terminalBootSceneService = currentService;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _terminalBootPhysicsService = currentService;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _terminalBootAudioService = currentService;
                    CacheSceneTransitionAudioBridge(currentService);
                    break;
                case GlobalRegistryServiceSlot.CameraJuiceRuntime:
                    _cameraJuiceSystem = currentService as ICameraJuiceSystem;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    break;
            }
        }

        private void RefreshTerminalBootServiceHandlesCold()
        {
            _terminalBootDispatcherService = GlobalRegistry.Dispatcher;
            _terminalBootTickService = GlobalRegistry.TickManager;
            _terminalBootSceneService = GlobalRegistry.Scene;
            _terminalBootPhysicsService = GlobalRegistry.Physics;
            _terminalBootAudioService = GlobalRegistry.Audio;
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            CacheSceneTransitionAudioBridge(GlobalRegistry.Audio);
            _cameraJuiceSystem = GlobalRegistry.CameraJuice;
        }

        private void ClearTerminalBootServiceHandles()
        {
            _terminalBootDispatcherService = null;
            _terminalBootTickService = null;
            _terminalBootSceneService = null;
            _terminalBootPhysicsService = null;
            _terminalBootAudioService = null;
            _tickDispatcher = null;
            _sceneTransitionAudioRuntime = null;
            _sceneTransitionAudioBridge = null;
            _cameraJuiceSystem = null;
        }

        private void CacheSceneTransitionAudioBridge(object audioRuntime)
        {
            if (!IsAudioRuntimeObjectUsable(audioRuntime))
            {
                _sceneTransitionAudioRuntime = null;
                _sceneTransitionAudioBridge = null;
                return;
            }

            _sceneTransitionAudioRuntime = audioRuntime;
            _sceneTransitionAudioBridge = audioRuntime as ISceneTransitionAudioBridge;
        }

        private ISceneTransitionAudioBridge ResolveSceneTransitionAudioBridge()
        {
            object audioRuntime = _sceneTransitionAudioRuntime;
            if (!IsAudioRuntimeObjectUsable(audioRuntime))
            {
                _sceneTransitionAudioRuntime = null;
                _sceneTransitionAudioBridge = null;
                return null;
            }

            ISceneTransitionAudioBridge sceneTransitionAudioBridge = _sceneTransitionAudioBridge;
            if (ReferenceEquals(sceneTransitionAudioBridge, audioRuntime) && IsAudioRuntimeObjectUsable(sceneTransitionAudioBridge))
                return sceneTransitionAudioBridge;

            sceneTransitionAudioBridge = audioRuntime as ISceneTransitionAudioBridge;
            _sceneTransitionAudioBridge = sceneTransitionAudioBridge;
            return IsAudioRuntimeObjectUsable(sceneTransitionAudioBridge) ? sceneTransitionAudioBridge : null;
        }

        private static bool IsAudioRuntimeObjectUsable(object runtime)
        {
            if (runtime == null)
                return false;

            if (runtime is IAudioService audioService && !audioService.IsAudioRuntimeReady)
                return false;

            if (runtime is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
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

        private bool TryRegisterSceneService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredSceneService)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            ISceneService registeredScene = GlobalRegistry.Scene;
            if (!ReferenceEquals(registeredScene, null) && !ReferenceEquals(registeredScene, this))
            {
                SceneRuntimeService staleRuntime = registeredScene as SceneRuntimeService;
                if (ReferenceEquals(staleRuntime, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterSceneService(registeredScene);
                GlobalRegistry.ClearSceneRuntime(staleRuntime);
                staleRuntime._registeredSceneService = false;
                staleRuntime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterSceneService(this);
            _registeredSceneService = ReferenceEquals(GlobalRegistry.Scene, this);
            _runtimeOwnerAborted = !_registeredSceneService;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _registeredSceneService;
        }

        private bool EnsureRuntimeOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            SceneRuntimeService runtime = GlobalRegistry.SceneRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                GlobalRegistry.ClearSceneRuntime(runtime);
                runtime._registeredSceneService = false;
                runtime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterSceneRuntime(this);
            return ReferenceEquals(GlobalRegistry.SceneRuntime, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            SceneRuntimeService runtime = GlobalRegistry.SceneRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsSceneRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.ClearSceneRuntime(runtime);
                runtime._registeredSceneService = false;
                runtime._isInitialized = false;
            }

            ISceneService registeredScene = GlobalRegistry.Scene;
            if (ReferenceEquals(registeredScene, null) || ReferenceEquals(registeredScene, this))
                return false;

            if (IsSceneServiceUsable(registeredScene))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            SceneRuntimeService staleRuntime = registeredScene as SceneRuntimeService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterSceneService(registeredScene);
                GlobalRegistry.ClearSceneRuntime(staleRuntime);
                staleRuntime._registeredSceneService = false;
                staleRuntime._isInitialized = false;
            }

            return false;
        }

        private static SceneRuntimeService ResolveUsableRuntime()
        {
            SceneRuntimeService runtime = GlobalRegistry.SceneRuntime;
            if (IsSceneRuntimeUsable(runtime))
                return runtime;

            if (!ReferenceEquals(runtime, null))
            {
                GlobalRegistry.ClearSceneRuntime(runtime);
                runtime._registeredSceneService = false;
                runtime._isInitialized = false;
            }

            ISceneService registeredScene = GlobalRegistry.Scene;
            if (IsSceneServiceUsable(registeredScene))
                return registeredScene as SceneRuntimeService;

            SceneRuntimeService staleRuntime = registeredScene as SceneRuntimeService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterSceneService(registeredScene);
                GlobalRegistry.ClearSceneRuntime(staleRuntime);
                staleRuntime._registeredSceneService = false;
                staleRuntime._isInitialized = false;
            }

            return null;
        }

        private static bool IsSceneServiceUsable(ISceneService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            SceneRuntimeService runtime = service as SceneRuntimeService;
            return ReferenceEquals(runtime, null) ||
                   (runtime._registeredSceneService && IsSceneRuntimeUsable(runtime));
        }

        private static bool IsSceneRuntimeUsable(SceneRuntimeService runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
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

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
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
