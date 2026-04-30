using System;
using System.IO;
using System.Text;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.AI;
using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Dev;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Input;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Deterministic bootstrap owner for the GlobalRegistry core and guarded scene routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29980)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string FatalBootCrashFileName = "fatal_boot_crash.log";
        private const string BootstrapAudioListenerRuntimeName = "[BootstrapAudioListener]";
        private const string PrefabRegistryRuntimeName = "[PrefabRegistry]";
        private const string PersistentWorldRegistryRuntimeName = "[PersistentWorldRegistry]";
        private const string RuntimePerformanceProfilerRuntimeName = "[RuntimePerformanceProfiler]";
        private const string CrashTelemetryRuntimeName = "[CrashTelemetryBuffer]";
        private const string HeadlessCommandLineArg = "-headless";
        private const int OptionalServiceTimeoutMilliseconds = 2000;
        private const string FatalBootOverlayMessageTemplate =
            "BIOS ERROR 0xBOOT_FATAL\nPHASE: {0}\nACTION: SEE fatal_boot_crash.log";
        private const int FatalBootCrashLogBufferBytes = 24576;
        private const string BiosErrorMessageTemplate =
            "BIOS ERROR 0xBOOT\nEXPECTED: 00_BOOTSTRAP [0]\nDETECTED: {0} [{1}]\nACTION: FORCED RECOVERY";
        private const int BootstrapSceneLoadWatchdogFrames = 1200;
        private const int BootstrapJobWaitWatchdogFrames = 1200;
        private static readonly UTF8Encoding _fatalBootCrashEncoding = new UTF8Encoding(false);

        private enum BootstrapPhase : byte
        {
            HardwareCheck = 0,
            MemoryPreWarm = 1,
            CoreServices = 2,
            Environment = 3,
            Player = 4,
            UI = 5,
            SceneActivate = 6,
            Complete = 7,
            Fatal = 8,
        }

        private enum BootstrapDependencyNode : byte
        {
            SystemDispatcher = 0,
            GameTickManager = 1,
            SaveManager = 2,
            ObjectPoolManager = 3,
            RenderDispatcher = 4,
            SceneRuntimeService = 5,
            EquipmentInteractionHandler = 6,
            HectonFloatingOrigin = 7,
            GlobalPhysicsStateManager = 8,
            PhysicsApplySystem = 9,
            DebrisManager = 10,
            EnvironmentRuntimeContextService = 11,
            OceanKinematicsRuntimeService = 12,
            EcosystemDirector = 13,
            FaunaSimulation = 14,
            SpatialAudioManager = 15,
            InputDispatcher = 16,
            PlayerRuntimeContextService = 17,
            PlayerInventoryManager = 18,
            PlayerSensoryManager = 19,
            PowerGridManager = 20,
            ConstructionManager = 21,
            Count = 22,
        }

        private readonly struct BootstrapDependencyEdge
        {
            public readonly BootstrapDependencyNode Source;
            public readonly BootstrapDependencyNode Dependency;

            public BootstrapDependencyEdge(BootstrapDependencyNode source, BootstrapDependencyNode dependency)
            {
                Source = source;
                Dependency = dependency;
            }
        }

        private static readonly string[] _bootstrapDependencyNodeNames =
        {
            "SystemDispatcher",
            "GameTickManager",
            "SaveManager",
            "ObjectPoolManager",
            "RenderDispatcher",
            "SceneRuntimeService",
            "EquipmentInteractionHandler",
            "HectonFloatingOrigin",
            "GlobalPhysicsStateManager",
            "PhysicsApplySystem",
            "DebrisManager",
            "EnvironmentRuntimeContextService",
            "OceanKinematicsRuntimeService",
            "EcosystemDirector",
            "FaunaSimulation",
            "SpatialAudioManager",
            "InputDispatcher",
            "PlayerRuntimeContextService",
            "PlayerInventoryManager",
            "PlayerSensoryManager",
            "PowerGridManager",
            "ConstructionManager",
        };

        private static readonly BootstrapDependencyEdge[] _bootstrapDependencyEdges =
        {
            new BootstrapDependencyEdge(BootstrapDependencyNode.GameTickManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.SaveManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.ObjectPoolManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.RenderDispatcher, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.SceneRuntimeService, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.EquipmentInteractionHandler, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.HectonFloatingOrigin, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.GlobalPhysicsStateManager, BootstrapDependencyNode.HectonFloatingOrigin),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PhysicsApplySystem, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PhysicsApplySystem, BootstrapDependencyNode.GlobalPhysicsStateManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.DebrisManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.DebrisManager, BootstrapDependencyNode.ObjectPoolManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.DebrisManager, BootstrapDependencyNode.PhysicsApplySystem),
            new BootstrapDependencyEdge(BootstrapDependencyNode.EnvironmentRuntimeContextService, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.OceanKinematicsRuntimeService, BootstrapDependencyNode.EnvironmentRuntimeContextService),
            new BootstrapDependencyEdge(BootstrapDependencyNode.EcosystemDirector, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.FaunaSimulation, BootstrapDependencyNode.EcosystemDirector),
            new BootstrapDependencyEdge(BootstrapDependencyNode.FaunaSimulation, BootstrapDependencyNode.PhysicsApplySystem),
            new BootstrapDependencyEdge(BootstrapDependencyNode.SpatialAudioManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PowerGridManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.ConstructionManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.ConstructionManager, BootstrapDependencyNode.SaveManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.ConstructionManager, BootstrapDependencyNode.ObjectPoolManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.ConstructionManager, BootstrapDependencyNode.PowerGridManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.InputDispatcher, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PlayerRuntimeContextService, BootstrapDependencyNode.InputDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PlayerInventoryManager, BootstrapDependencyNode.PlayerRuntimeContextService),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PlayerInventoryManager, BootstrapDependencyNode.ObjectPoolManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PlayerSensoryManager, BootstrapDependencyNode.PlayerRuntimeContextService),
        };

        private static GameBootstrapper _instance;
        private static bool _isBootstrapComplete;
        private static bool _sceneGuardRegistered;
        private static bool _entryRecoveryIssued;
        private static BootstrapPhase _currentPhase;
        private static InputManager _bootstrapInputManager;
        private static bool _headlessBootMode;
        private static bool _preWarmAssetsReady;
        [Header("Bootstrap Prewarm")]
        [Tooltip("Shader variant collections warmed during MemoryPreWarm before scene or player activation.")]
        [SerializeField] private ShaderVariantCollection[] shaderVariantCollections;

        private bool _bootstrapRunInProgress;
        private bool _sceneActivationRunInProgress;
        private SceneBootstrap _pendingSceneBootstrap;
        // COLD ALLOC: BootstrapDependencyNode[22] - cached Kahn topological service execution order - owner: GameBootstrapper
        private readonly BootstrapDependencyNode[] _bootstrapExecutionOrder = new BootstrapDependencyNode[(int)BootstrapDependencyNode.Count];
        private int _bootstrapExecutionOrderCount;

        /// <summary>
        /// Fires after the unified bootstrap state machine has completed its core phases.
        /// </summary>
        public static event Action OnBootstrapComplete;

        /// <summary>
        /// True once the bootstrap core finished its ordered initialization phases.
        /// </summary>
        public static bool IsBootstrapComplete => _isBootstrapComplete;

        /// <summary>
        /// True when boot is running in data-only server/testing mode.
        /// </summary>
        public static bool IsHeadlessBootMode => _headlessBootMode;

        /// <summary>
        /// True once bootstrap shader and residency prewarm gates have completed.
        /// </summary>
        public static bool ArePreWarmAssetsReady => _preWarmAssetsReady;

        /// <summary>
        /// True when all mandatory core services are registered and scene routing may proceed.
        /// </summary>
        public static bool AreAllSystemsReady()
        {
            return _isBootstrapComplete &&
                   GlobalRegistry.Dispatcher != null &&
                   GlobalRegistry.TickManager != null &&
                   GlobalRegistry.Save != null &&
                   GlobalRegistry.ObjectPool != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isBootstrapComplete = false;
            _entryRecoveryIssued = false;
            _currentPhase = BootstrapPhase.HardwareCheck;
            _bootstrapInputManager = null;
            _headlessBootMode = false;
            _preWarmAssetsReady = false;
            OnBootstrapComplete = null;

            if (_sceneGuardRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoadedGuard;
                _sceneGuardRegistered = false;
            }

            BootstrapBiosErrorOverlay.Hide();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void GuardInitialSceneEntry()
        {
            if (!Application.isPlaying || _isBootstrapComplete)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (TryRecoverEntryVector(activeScene, true) && IsBootstrapScene(activeScene))
                EnsureRuntimeInstance()?.BeginBootstrap();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void GuardEntryVectorBeforeSceneLoad()
        {
            if (!Application.isPlaying || _isBootstrapComplete)
                return;

            TryRecoverEntryVector(SceneManager.GetActiveScene(), true);
        }

        /// <summary>
        /// Ensures a runtime bootstrap owner exists on the current bootstrap shell object.
        /// </summary>
        /// <param name="owner">Bootstrap shell owner.</param>
        /// <returns>Live bootstrap component.</returns>
        public static GameBootstrapper EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameBootstrapper existing = UnityEngine.Object.FindAnyObjectByType<GameBootstrapper>();
            if (existing != null)
                return existing;

            Scene activeScene = SceneManager.GetActiveScene();
            GameObject runtimeRoot = new GameObject("[GameBootstrapper]"); // COLD ALLOC: GameObject[1] - bootstrap authority root when scene authoring omitted it - owner: GameBootstrapper
            if (activeScene.IsValid())
                SceneManager.MoveGameObjectToScene(runtimeRoot, activeScene);

            return runtimeRoot.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - unified bootstrap authority - owner: GameBootstrapper
        }

        /// <summary>
        /// Ensures a runtime bootstrap owner exists on the current bootstrap shell object.
        /// </summary>
        /// <param name="owner">Bootstrap shell owner.</param>
        /// <returns>Live bootstrap component.</returns>
        public static GameBootstrapper EnsureRuntimeInstance(GameObject owner)
        {
            if (_instance != null)
                return _instance;

            if (owner == null)
                return EnsureRuntimeInstance();

            if (!owner.TryGetComponent(out GameBootstrapper bootstrapper))
                bootstrapper = owner.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - deterministic bootstrap owner on 00_BOOTSTRAP shell - owner: BootstrapController

            return bootstrapper;
        }

        /// <summary>
        /// Executes the ordered bootstrap phases once.
        /// </summary>
        public bool InitializeBootstrap()
        {
            BeginBootstrap();
            return _isBootstrapComplete || _bootstrapRunInProgress;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Scene activeScene = gameObject.scene;
            if (Application.isPlaying && IsBootstrapScene(activeScene))
                BeginBootstrap();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Starts the unified Awaitable bootstrap state machine if it has not already run.
        /// </summary>
        public void BeginBootstrap()
        {
            if (_isBootstrapComplete || _bootstrapRunInProgress)
                return;

            EnsureCrashTelemetryBufferRegistered();
            _bootstrapRunInProgress = true;
            _ = RunBootstrapStateMachineAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Requests gameplay scene activation through the unified bootstrap owner.
        /// </summary>
        /// <param name="sceneBootstrap">Scene activation executor in the loaded scene.</param>
        public static void RequestSceneActivation(SceneBootstrap sceneBootstrap)
        {
            if (sceneBootstrap == null)
                return;

            GameBootstrapper bootstrapper = EnsureRuntimeInstance();
            if (bootstrapper == null)
                return;

            bootstrapper.ScheduleSceneActivation(sceneBootstrap);
        }

        private void ScheduleSceneActivation(SceneBootstrap sceneBootstrap)
        {
            if (sceneBootstrap == null)
                return;

            _pendingSceneBootstrap = sceneBootstrap;
            if (!_isBootstrapComplete || _sceneActivationRunInProgress)
                return;

            _sceneActivationRunInProgress = true;
            _ = RunSceneActivationAsync(sceneBootstrap, destroyCancellationToken);
        }

        private async Awaitable<bool> RunBootstrapStateMachineAsync(CancellationToken ownerToken)
        {
            BootstrapStatus.BeginBoot();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
            CancellationToken ct = cts.Token;

            try
            {
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.HardwareCheck, BootstrapStepToken.HardwareCheck, InitializeHardwareCheckPhaseAsync, ct))
                    return false;
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.MemoryPreWarm, BootstrapStepToken.MemoryPreWarm, InitializeMemoryPreWarmPhaseAsync, ct))
                    return false;
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.CoreServices, BootstrapStepToken.CoreServices, InitializeCoreServicesPhaseAsync, ct))
                    return false;
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.Environment, BootstrapStepToken.Environment, InitializeEnvironmentPhaseAsync, ct))
                    return false;
                if (!_headlessBootMode &&
                    !await RunBootstrapPhaseAsync(BootstrapPhase.Player, BootstrapStepToken.Player, InitializePlayerPhaseAsync, ct))
                {
                    return false;
                }

                if (!_headlessBootMode &&
                    !await RunBootstrapPhaseAsync(BootstrapPhase.UI, BootstrapStepToken.UI, InitializeUIPhaseAsync, ct))
                {
                    return false;
                }

                EnsureExtendedRegistryCoverageForActiveScene();
                _isBootstrapComplete = true;
                BootstrapBiosErrorOverlay.Hide();
                OnBootstrapComplete?.Invoke();

                if (!await RunBootstrapPhaseAsync(BootstrapPhase.SceneActivate, BootstrapStepToken.SceneActivate, InitializeSceneActivatePhaseAsync, ct))
                    return false;

                _currentPhase = BootstrapPhase.Complete;
                return true;
            }
            catch (OperationCanceledException)
            {
                _currentPhase = BootstrapPhase.Fatal;
                return false;
            }
            catch (Exception exception)
            {
                _currentPhase = BootstrapPhase.Fatal;
                HandleFatalBootstrapException("BootstrapEntry", exception);
                return false;
            }
            finally
            {
                _bootstrapRunInProgress = false;
            }
        }

        private async Awaitable<bool> RunBootstrapPhaseAsync(
            BootstrapPhase phase,
            BootstrapStepToken stepToken,
            Func<CancellationToken, Awaitable<bool>> phaseAction,
            CancellationToken ct)
        {
            _currentPhase = phase;
            BootstrapStatus.BeginStep(stepToken);
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            try
            {
                bool phaseComplete = phaseAction == null || await phaseAction(ct);
                if (!phaseComplete)
                    _currentPhase = BootstrapPhase.Fatal;

                return phaseComplete;
            }
            catch (OperationCanceledException)
            {
                _currentPhase = BootstrapPhase.Fatal;
                throw;
            }
            catch (Exception exception)
            {
                _currentPhase = BootstrapPhase.Fatal;
                HandleFatalBootstrapException(phase.ToString(), exception);
                return false;
            }
            finally
            {
                phaseStopwatch.Stop();
                double elapsedMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
                CrashTelemetryBuffer.RecordBootstrapPhaseDuration(stepToken, elapsedMilliseconds);
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private async Awaitable<bool> InitializeHardwareCheckPhaseAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _headlessBootMode = HasCommandLineArg(HeadlessCommandLineArg);
            global::Hecton8.Core.HectonHardwareProfile hardwareProfile = CaptureHardwareProfile();
            GlobalRegistry.RegisterHardwareProfile(in hardwareProfile);

            Scene activeScene = SceneManager.GetActiveScene();
            if (!TryRecoverEntryVector(activeScene, false))
                return false;

            RegisterSceneLoadGuard();
            if (!_headlessBootMode)
                EnsureBootstrapAudioListener(activeScene);

            bool dependencyGraphValid = TryBuildBootstrapDependencyExecutionOrder(
                _bootstrapExecutionOrder,
                out _bootstrapExecutionOrderCount);
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return dependencyGraphValid;
        }

        private async Awaitable<bool> InitializeMemoryPreWarmPhaseAsync(CancellationToken ct)
        {
            _preWarmAssetsReady = false;
            NativeArenaAllocator.Initialize();
            if (!_headlessBootMode)
            {
                VRAMEnforcer.InitializeRuntimeBudget();
                SceneInstantiationGate.EnsureRuntimeInstance();
            }

            if (!await WarmConfiguredShaderVariantCollectionsAsync(ct))
                return false;

            _preWarmAssetsReady = true;
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return true;
        }

        private async Awaitable<bool> InitializeCoreServicesPhaseAsync(CancellationToken ct)
        {
            bool initialized = InitializeCoreLayer();
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return initialized;
        }

        private async Awaitable<bool> InitializeEnvironmentPhaseAsync(CancellationToken ct)
        {
            bool initialized = InitializeEnvironmentLayer();
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return initialized;
        }

        private async Awaitable<bool> InitializePlayerPhaseAsync(CancellationToken ct)
        {
            bool initialized = InitializePlayerLayer();
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return initialized;
        }

        private async Awaitable<bool> InitializeUIPhaseAsync(CancellationToken ct)
        {
            InitializeUILayer();
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return true;
        }

        private async Awaitable<bool> InitializeSceneActivatePhaseAsync(CancellationToken ct)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (IsBootstrapScene(activeScene))
            {
                GameStartContextHolder.Reset();
                if (_headlessBootMode)
                {
                    BootstrapStatus.MarkMainMenuReached();
                    return true;
                }

                return await LoadMainMenuAsync(ct);
            }

            SceneBootstrap sceneBootstrap = _pendingSceneBootstrap;
            if (sceneBootstrap == null)
                sceneBootstrap = UnityEngine.Object.FindAnyObjectByType<SceneBootstrap>();

            if (sceneBootstrap == null)
                return true;

            return await ExecuteSceneActivationAsync(sceneBootstrap, ct);
        }

        private async Awaitable<bool> RunSceneActivationAsync(SceneBootstrap sceneBootstrap, CancellationToken ownerToken)
        {
            try
            {
                return await ExecuteSceneActivationAsync(sceneBootstrap, ownerToken);
            }
            finally
            {
                if (ReferenceEquals(_pendingSceneBootstrap, sceneBootstrap))
                    _pendingSceneBootstrap = null;

                _sceneActivationRunInProgress = false;
            }
        }

        private static async Awaitable<bool> ExecuteSceneActivationAsync(SceneBootstrap sceneBootstrap, CancellationToken ct)
        {
            if (sceneBootstrap == null)
                return false;

            return await sceneBootstrap.ExecuteSceneActivationAsync(ct);
        }

        private static async Awaitable<bool> LoadMainMenuAsync(CancellationToken ct)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
                return false;

            loadOperation.allowSceneActivation = false;
            int waitFrames = 0;
            while (loadOperation.progress < 0.9f)
            {
                ct.ThrowIfCancellationRequested();
                if (waitFrames >= BootstrapSceneLoadWatchdogFrames)
                {
                    LogBootstrapSceneLoadWatchdog("main-menu load", loadOperation.progress, waitFrames);
                    return false;
                }

                waitFrames++;
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }

            if (!await WaitForBootstrapActivationGatesAsync(ct))
                return false;

            loadOperation.allowSceneActivation = true;
            waitFrames = 0;
            while (!loadOperation.isDone)
            {
                ct.ThrowIfCancellationRequested();
                if (waitFrames >= BootstrapSceneLoadWatchdogFrames)
                {
                    LogBootstrapSceneLoadWatchdog("main-menu activation", loadOperation.progress, waitFrames);
                    return false;
                }

                waitFrames++;
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }

            BootstrapStatus.MarkMainMenuReached();
            return true;
        }

        private static void LogBootstrapSceneLoadWatchdog(string stageName, float progress, int waitFrames)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Scene load watchdog tripped during {stageName}. progress={progress:0.000} frames={waitFrames} target={MainMenuSceneName}.");
#endif
        }

        private bool InitializeCoreLayer()
        {
            EnsureCrashTelemetryBufferRegistered();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimePerformanceProfilerRegistered();
#endif
            ThreadSafeCommandQueue.Initialize();
            EnsurePrefabRegistry();
            EnsurePersistentWorldRegistry();
            return InitializeBootstrapLayerNodes(BootstrapPhase.CoreServices);
        }

        private bool InitializeEnvironmentLayer()
        {
            return InitializeBootstrapLayerNodes(BootstrapPhase.Environment);
        }

        private bool InitializePlayerLayer()
        {
            if (!InputManager.TryValidateRuntimeConfiguration(out string inputConfigurationError))
            {
                BootstrapBiosErrorOverlay.Show(inputConfigurationError);
                return false;
            }

            InputManager inputManager = UnityEngine.Object.FindAnyObjectByType<InputManager>(FindObjectsInactive.Include);
            if (inputManager == null)
            {
                GameObject inputRoot = new GameObject("[InputManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned native input owner - owner: GameBootstrapper
                inputManager = inputRoot.AddComponent<InputManager>();
            }

            if (inputManager == null)
            {
                BootstrapBiosErrorOverlay.Show(
                    "BIOS ERROR 0xINPUT\nEXPECTED: Runtime InputManager instance\nDETECTED: explicit bootstrap input owner resolution failed\nACTION: Repair the bootstrap input owner before boot.");
                return false;
            }

            if (!inputManager.TryValidateRuntimeActions(out string inputActionsError))
            {
                BootstrapBiosErrorOverlay.Show(inputActionsError);
                return false;
            }

            _bootstrapInputManager = inputManager;

            if (Application.isPlaying)
                DontDestroyOnLoad(inputManager.gameObject);

            ContextualPhysicalIkRuntime.EnsureRuntimeInstance();
            if (!InitializeBootstrapLayerNodes(BootstrapPhase.Player))
                return false;

            PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
            playerContextService.RefreshRuntimeContext();
            return true;
        }

        private void InitializeUILayer()
        {
            // No UI-layer GlobalRegistry adapter exists yet.
            // Existing menu/HUD ownership remains on scene-authored controllers.
        }

        private async Awaitable<bool> WarmConfiguredShaderVariantCollectionsAsync(CancellationToken ct)
        {
            if (_headlessBootMode)
                return true;

            int collectionCount = shaderVariantCollections != null ? shaderVariantCollections.Length : 0;
            for (int i = 0; i < collectionCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                ShaderVariantCollection collection = shaderVariantCollections[i];
                if (collection == null)
                    continue;

                collection.WarmUp();
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }

            await Awaitable.NextFrameAsync(cancellationToken: ct);
            await Awaitable.NextFrameAsync(cancellationToken: ct);
            return true;
        }

        private static bool AreBootstrapActivationGatesReady()
        {
            if (!_preWarmAssetsReady)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            return registry != null && registry.AreResidentWorldPrefabPoolsReady();
        }

        private static async Awaitable<bool> WaitForBootstrapActivationGatesAsync(CancellationToken ct)
        {
            int waitFrames = 0;
            while (!AreBootstrapActivationGatesReady())
            {
                ct.ThrowIfCancellationRequested();
                if (waitFrames >= BootstrapJobWaitWatchdogFrames)
                {
                    LogBootstrapSceneLoadWatchdog("asset activation gates", 0.9f, waitFrames);
                    return false;
                }

                waitFrames++;
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }

            return true;
        }

        private bool InitializeBootstrapLayerNodes(BootstrapPhase phase)
        {
            if (_bootstrapExecutionOrderCount != (int)BootstrapDependencyNode.Count &&
                !TryBuildBootstrapDependencyExecutionOrder(_bootstrapExecutionOrder, out _bootstrapExecutionOrderCount))
            {
                return false;
            }

            for (int orderIndex = 0; orderIndex < _bootstrapExecutionOrderCount; orderIndex++)
            {
                BootstrapDependencyNode node = _bootstrapExecutionOrder[orderIndex];
                if (ResolveBootstrapNodePhase(node) != phase)
                    continue;

                if (!InitializeBootstrapDependencyNode(node))
                    return false;
            }

            return true;
        }

        private static BootstrapPhase ResolveBootstrapNodePhase(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher:
                case BootstrapDependencyNode.GameTickManager:
                case BootstrapDependencyNode.SaveManager:
                case BootstrapDependencyNode.ObjectPoolManager:
                case BootstrapDependencyNode.RenderDispatcher:
                case BootstrapDependencyNode.SceneRuntimeService:
                case BootstrapDependencyNode.EquipmentInteractionHandler:
                    return BootstrapPhase.CoreServices;

                case BootstrapDependencyNode.HectonFloatingOrigin:
                case BootstrapDependencyNode.GlobalPhysicsStateManager:
                case BootstrapDependencyNode.PhysicsApplySystem:
                case BootstrapDependencyNode.DebrisManager:
                case BootstrapDependencyNode.EnvironmentRuntimeContextService:
                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                case BootstrapDependencyNode.EcosystemDirector:
                case BootstrapDependencyNode.FaunaSimulation:
                case BootstrapDependencyNode.SpatialAudioManager:
                case BootstrapDependencyNode.PowerGridManager:
                case BootstrapDependencyNode.ConstructionManager:
                    return BootstrapPhase.Environment;

                case BootstrapDependencyNode.InputDispatcher:
                case BootstrapDependencyNode.PlayerRuntimeContextService:
                case BootstrapDependencyNode.PlayerInventoryManager:
                case BootstrapDependencyNode.PlayerSensoryManager:
                    return BootstrapPhase.Player;

                default:
                    return BootstrapPhase.Fatal;
            }
        }

        private static bool InitializeBootstrapDependencyNode(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher:
                    return EnsureSystemDispatcherRegistered() != null && GlobalRegistry.Dispatcher != null;

                case BootstrapDependencyNode.GameTickManager:
                    return EnsureGameTickManagerRegistered() != null && GlobalRegistry.TickManager != null;

                case BootstrapDependencyNode.SaveManager:
                    return EnsureSaveServiceRegistered() != null && GlobalRegistry.Save != null;

                case BootstrapDependencyNode.ObjectPoolManager:
                    return EnsureObjectPoolServiceRegistered() != null && GlobalRegistry.ObjectPool != null;

                case BootstrapDependencyNode.RenderDispatcher:
                    if (_headlessBootMode)
                        return true;

                    return EnsureRenderDispatcherRegistered() != null && GlobalRegistry.RenderDispatcher != null;

                case BootstrapDependencyNode.SceneRuntimeService:
                {
                    SceneRuntimeService sceneRuntimeService = SceneRuntimeService.EnsureRuntimeInstance();
                    if (sceneRuntimeService == null)
                        return false;

                    sceneRuntimeService.InitializeService();
                    return GlobalRegistry.Scene != null;
                }

                case BootstrapDependencyNode.EquipmentInteractionHandler:
                    return EnsureEquipmentInteractionServiceRegistered() != null && GlobalRegistry.InteractionSignals != null;

                case BootstrapDependencyNode.HectonFloatingOrigin:
                    return EnsureFloatingOriginRegistered() != null && HectonFloatingOrigin.Instance != null;

                case BootstrapDependencyNode.GlobalPhysicsStateManager:
                    return EnsureGlobalPhysicsStateManagerRegistered() != null && GlobalRegistry.PhysicsStateManager != null;

                case BootstrapDependencyNode.PhysicsApplySystem:
                {
                    PhysicsApplySystem physicsApplySystem = PhysicsApplySystem.EnsureRuntimeInstance();
                    if (physicsApplySystem == null)
                        return false;

                    physicsApplySystem.InitializeService();
                    return GlobalRegistry.Physics != null;
                }

                case BootstrapDependencyNode.DebrisManager:
                {
                    DebrisManager debrisManager = DebrisManager.EnsureRuntimeInstance();
                    if (debrisManager == null)
                        return false;

                    debrisManager.InitializeService();
                    return GlobalRegistry.Debris != null;
                }

                case BootstrapDependencyNode.EnvironmentRuntimeContextService:
                {
                    EnvironmentRuntimeContextService environmentContextService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
                    if (environmentContextService == null)
                        return false;

                    environmentContextService.InitializeService();
                    return GlobalRegistry.Environment != null;
                }

                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                {
                    OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
                    if (oceanKinematicsRuntimeService == null)
                        return false;

                    oceanKinematicsRuntimeService.InitializeService();
                    return GlobalRegistry.OceanKinematics != null;
                }

                case BootstrapDependencyNode.EcosystemDirector:
                    return EnsureEcosystemDirectorRegistered() != null && GlobalRegistry.EcosystemDirector != null;

                case BootstrapDependencyNode.FaunaSimulation:
                    return EnsureFaunaSimulationRegistered();

                case BootstrapDependencyNode.SpatialAudioManager:
                    if (_headlessBootMode)
                        return TryRegisterNoOpAudioFallback("Headless boot skips SpatialAudioManager");

                    return InitializeSpatialAudioBootstrapNode();

                case BootstrapDependencyNode.ConstructionManager:
                {
                    ConstructionManager constructionManager = EnsureConstructionServiceRegistered();
                    return constructionManager == null || GlobalRegistry.ConstructionRuntime != null;
                }

                case BootstrapDependencyNode.InputDispatcher:
                    return EnsureInputDispatcherRegistered() != null && GlobalRegistry.Input != null;

                case BootstrapDependencyNode.PlayerRuntimeContextService:
                {
                    PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
                    if (playerContextService == null)
                        return false;

                    playerContextService.InitializeServiceDeferredSync();
                    return GlobalRegistry.Player != null;
                }

                case BootstrapDependencyNode.PlayerInventoryManager:
                {
                    PlayerInventoryManager playerInventoryManager = PlayerInventoryManager.EnsureRuntimeInstance();
                    if (playerInventoryManager == null)
                        return false;

                    playerInventoryManager.InitializeService();
                    return GlobalRegistry.PlayerInventory != null;
                }

                case BootstrapDependencyNode.PlayerSensoryManager:
                {
                    PlayerSensoryManager playerSensoryManager = PlayerSensoryManager.EnsureRuntimeInstance();
                    if (playerSensoryManager == null)
                        return false;

                    playerSensoryManager.InitializeService();
                    return GlobalRegistry.PlayerSensory != null;
                }

                case BootstrapDependencyNode.PowerGridManager:
                    return EnsurePowerGridServiceRegistered() != null && GlobalRegistry.PowerGrid != null;

                default:
                    return false;
            }
        }

        private static SystemDispatcher EnsureSystemDispatcherRegistered()
        {
            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher == null)
                dispatcher = UnityEngine.Object.FindAnyObjectByType<SystemDispatcher>();

            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[SystemDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned gameplay dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<SystemDispatcher>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(dispatcher.gameObject);

            dispatcher.InitializeService();
            return dispatcher;
        }

        private static GameTickManager EnsureGameTickManagerRegistered()
        {
            GameTickManager tickManager = GlobalRegistry.TickManager;
            if (tickManager == null)
                tickManager = UnityEngine.Object.FindAnyObjectByType<GameTickManager>();

            if (tickManager == null)
            {
                GameObject runtimeRoot = new GameObject("[GameTickManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned tick manager root - owner: GameBootstrapper
                tickManager = runtimeRoot.AddComponent<GameTickManager>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(tickManager.gameObject);

            tickManager.InitializeService();
            return tickManager;
        }

        private static SaveManager EnsureSaveServiceRegistered()
        {
            SaveManager saveManager = GlobalRegistry.SaveRuntime;
            if (saveManager == null)
                saveManager = UnityEngine.Object.FindAnyObjectByType<SaveManager>();

            if (saveManager == null)
            {
                GameObject runtimeRoot = new GameObject("[SaveManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned save manager root - owner: GameBootstrapper
                saveManager = runtimeRoot.AddComponent<SaveManager>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(saveManager.gameObject);

            saveManager.InitializeService();
            return saveManager;
        }

        private static ObjectPoolManager EnsureObjectPoolServiceRegistered()
        {
            ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;
            if (objectPoolManager == null)
                objectPoolManager = UnityEngine.Object.FindAnyObjectByType<ObjectPoolManager>();

            if (objectPoolManager == null)
            {
                GameObject runtimeRoot = new GameObject("[ObjectPoolManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned object pool root - owner: GameBootstrapper
                objectPoolManager = runtimeRoot.AddComponent<ObjectPoolManager>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(objectPoolManager.gameObject);

            objectPoolManager.InitializeService();
            return objectPoolManager;
        }

        private static RenderDispatcher EnsureRenderDispatcherRegistered()
        {
            RenderDispatcher dispatcher = GlobalRegistry.RenderDispatcher;
            if (dispatcher == null)
                dispatcher = UnityEngine.Object.FindAnyObjectByType<RenderDispatcher>();

            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[RenderDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned SRP render dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<RenderDispatcher>();
            }

            dispatcher.InitializeService();
            return dispatcher;
        }

        private static EquipmentInteractionHandler EnsureEquipmentInteractionServiceRegistered()
        {
            if (GlobalRegistry.InteractionSignals is EquipmentInteractionHandler registeredHandler)
                return registeredHandler;

            EquipmentInteractionHandler interactionHandler = UnityEngine.Object.FindAnyObjectByType<EquipmentInteractionHandler>();
            if (interactionHandler == null)
            {
                GameObject runtimeRoot = new GameObject("[EquipmentInteractionHandler]"); // COLD ALLOC: GameObject[1] - bootstrap-owned interaction signal root - owner: GameBootstrapper
                interactionHandler = runtimeRoot.AddComponent<EquipmentInteractionHandler>();
            }

            interactionHandler.InitializeService();
            return interactionHandler;
        }

        private static CrashTelemetryBuffer EnsureCrashTelemetryBufferRegistered()
        {
            CrashTelemetryBuffer telemetry = CrashTelemetryBuffer.EnsureRuntimeInstance();
            if (telemetry == null)
            {
                GameObject runtimeRoot = new GameObject(CrashTelemetryRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned crash telemetry root - owner: GameBootstrapper
                telemetry = runtimeRoot.AddComponent<CrashTelemetryBuffer>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(telemetry.gameObject);

            return telemetry;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static RuntimePerformanceProfiler EnsureRuntimePerformanceProfilerRegistered()
        {
            RuntimePerformanceProfiler profiler = RuntimePerformanceProfiler.Instance;
            if (profiler == null)
            {
                GameObject runtimeRoot = new GameObject(RuntimePerformanceProfilerRuntimeName); // COLD ALLOC: GameObject[1] - development performance profiler root - owner: GameBootstrapper
                profiler = runtimeRoot.AddComponent<RuntimePerformanceProfiler>();
            }

            profiler.ConfigureForDevRun(
                autoStartOnEnable: true,
                enableBudgetViolationLogging: true,
                enableWindowLogging: false,
                sampleWindow: 2f);

            if (Application.isPlaying)
                DontDestroyOnLoad(profiler.gameObject);

            return profiler;
        }
#endif

        private static PrefabRegistry EnsurePrefabRegistry()
        {
            PrefabRegistry registry = PrefabRegistry.Instance;
            if (registry != null)
                return registry;

            GameObject runtimeRoot = new GameObject(PrefabRegistryRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned prefab registry fallback - owner: GameBootstrapper
            if (Application.isPlaying)
                DontDestroyOnLoad(runtimeRoot);

            return runtimeRoot.AddComponent<PrefabRegistry>();
        }

        private static PersistentWorldRegistry EnsurePersistentWorldRegistry()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                return registry;

            GameObject runtimeRoot = new GameObject(PersistentWorldRegistryRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned persistent world registry fallback - owner: GameBootstrapper
            if (Application.isPlaying)
                DontDestroyOnLoad(runtimeRoot);

            return runtimeRoot.AddComponent<PersistentWorldRegistry>();
        }

        private static HectonFloatingOrigin EnsureFloatingOriginRegistered()
        {
            HectonFloatingOrigin origin = HectonFloatingOrigin.Instance;
            if (origin == null)
                origin = UnityEngine.Object.FindAnyObjectByType<HectonFloatingOrigin>();

            if (origin == null)
            {
                GameObject runtimeRoot = new GameObject("[HectonFloatingOrigin]"); // COLD ALLOC: GameObject[1] - bootstrap-owned AUP/floating-origin authority for headless simulation - owner: GameBootstrapper
                origin = runtimeRoot.AddComponent<HectonFloatingOrigin>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(origin.gameObject);

            origin.InitializeService();
            return origin;
        }

        private static GlobalPhysicsStateManager EnsureGlobalPhysicsStateManagerRegistered()
        {
            GlobalPhysicsStateManager manager = GlobalRegistry.PhysicsStateManager;
            if (manager == null)
                manager = UnityEngine.Object.FindAnyObjectByType<GlobalPhysicsStateManager>();

            if (manager == null)
            {
                GameObject runtimeRoot = new GameObject("[GlobalPhysicsStateManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned global physics-state manager root - owner: GameBootstrapper
                manager = runtimeRoot.AddComponent<GlobalPhysicsStateManager>();
            }

            manager.InitializeService();
            return manager;
        }

        private static EcosystemDirector EnsureEcosystemDirectorRegistered()
        {
            EcosystemDirector director = EcosystemDirector.ActiveRuntimeInstance;
            if (director == null)
                director = UnityEngine.Object.FindAnyObjectByType<EcosystemDirector>();

            if (director == null)
            {
                GameObject runtimeRoot = new GameObject("[EcosystemDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned data-only ecosystem simulation owner - owner: GameBootstrapper
                director = runtimeRoot.AddComponent<EcosystemDirector>();
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(director.gameObject);

            director.InitializeService();
            return director;
        }

        private static bool EnsureFaunaSimulationRegistered()
        {
            IFaunaSim registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            if (registeredFaunaSimulation != null && registeredFaunaSimulation.IsReady)
                return true;

            FaunaDirector faunaDirector = FaunaDirector.ActiveRuntimeInstance;
            if (faunaDirector == null && !_headlessBootMode)
                faunaDirector = UnityEngine.Object.FindAnyObjectByType<FaunaDirector>();

            if (faunaDirector != null)
                faunaDirector.InitializeService();

            if (GlobalRegistry.FaunaSimulation != null)
                return GlobalRegistry.FaunaSimulation.IsReady;

            GlobalRegistry.RegisterFaunaSimulationService(DemiurgeFaunaSimulationService.Instance);
            return GlobalRegistry.FaunaSimulation != null && GlobalRegistry.FaunaSimulation.IsReady;
        }

        private static InputDispatcher EnsureInputDispatcherRegistered()
        {
            if (GlobalRegistry.Input is InputDispatcher registeredDispatcher)
            {
                registeredDispatcher.BindNativeInputManager(_bootstrapInputManager);
                return registeredDispatcher;
            }

            InputDispatcher dispatcher = UnityEngine.Object.FindAnyObjectByType<InputDispatcher>();
            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[InputDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned input dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<InputDispatcher>();
            }

            dispatcher.BindNativeInputManager(_bootstrapInputManager);
            dispatcher.InitializeService();
            return dispatcher;
        }

        private static PowerGridManager EnsurePowerGridServiceRegistered()
        {
            if (GlobalRegistry.PowerGrid is PowerGridManager registeredPowerGrid)
                return registeredPowerGrid;

            PowerGridManager powerGridManager = UnityEngine.Object.FindAnyObjectByType<PowerGridManager>();
            if (powerGridManager == null)
            {
                GameObject runtimeRoot = new GameObject("[PowerGridManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned power grid runtime root - owner: GameBootstrapper
                powerGridManager = runtimeRoot.AddComponent<PowerGridManager>();
            }

            powerGridManager.InitializeService();
            return powerGridManager;
        }

        private static ConstructionManager EnsureConstructionServiceRegistered()
        {
            if (GlobalRegistry.Logistics is ConstructionManager registeredConstruction)
                return registeredConstruction;

            ConstructionManager constructionManager = UnityEngine.Object.FindAnyObjectByType<ConstructionManager>();
            if (constructionManager == null)
                return null;

            constructionManager.InitializeService();
            return constructionManager;
        }

        private static SpatialAudioManager EnsureAudioServiceRegistered()
        {
            if (GlobalRegistry.Audio is SpatialAudioManager registeredAudioService)
                return registeredAudioService;

            SpatialAudioManager sceneAudioService = UnityEngine.Object.FindAnyObjectByType<SpatialAudioManager>();
            if (sceneAudioService != null)
                return sceneAudioService;

            GameObject runtimeRoot = new GameObject("[SpatialAudioManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned audio service root - owner: GameBootstrapper
            if (Application.isPlaying)
                DontDestroyOnLoad(runtimeRoot);

            return runtimeRoot.AddComponent<SpatialAudioManager>(); // COLD ALLOC: SpatialAudioManager[1] - bootstrap-owned audio service runtime - owner: GameBootstrapper
        }

        private static bool InitializeSpatialAudioBootstrapNode()
        {
            Stopwatch serviceStopwatch = Stopwatch.StartNew();
            try
            {
                SpatialAudioManager spatialAudioManager = EnsureAudioServiceRegistered();
                if (spatialAudioManager == null)
                    return TryRegisterNoOpAudioFallback("SpatialAudioManager missing");

                spatialAudioManager.InitializeService();
                serviceStopwatch.Stop();
                if (serviceStopwatch.ElapsedMilliseconds > OptionalServiceTimeoutMilliseconds)
                    LogOptionalBootstrapWarning("SpatialAudioManager exceeded the optional-service bootstrap budget.");

                if (GlobalRegistry.Audio != null)
                    return true;

                return TryRegisterNoOpAudioFallback("SpatialAudioManager did not register IAudioService");
            }
            catch (Exception exception)
            {
                serviceStopwatch.Stop();
                return TryRegisterNoOpAudioFallback(exception.Message);
            }
        }

        private static bool TryRegisterNoOpAudioFallback(string reason)
        {
            if (GlobalRegistry.Audio != null)
                return true;

            GlobalRegistry.RegisterAudioService(NoOpAudioService.Instance);
            LogOptionalBootstrapWarning($"Injected NoOp audio service. Reason: {reason}");
            return GlobalRegistry.Audio != null;
        }

        private static void LogOptionalBootstrapWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[GameBootstrapper] {message}");
#endif
        }

        private static bool HasCommandLineArg(string commandLineArg)
        {
            if (string.IsNullOrEmpty(commandLineArg))
                return false;

            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], commandLineArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static global::Hecton8.Core.HectonHardwareProfile CaptureHardwareProfile()
        {
            int graphicsMemoryMb = Mathf.Max(0, SystemInfo.graphicsMemorySize);
            int systemMemoryMb = Mathf.Max(0, SystemInfo.systemMemorySize);
            int processorCount = Mathf.Max(1, SystemInfo.processorCount);
            return new global::Hecton8.Core.HectonHardwareProfile(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                ResolveQualityTier(graphicsMemoryMb, systemMemoryMb, processorCount));
        }

        private static global::Hecton8.Core.HectonQualityTier ResolveQualityTier(int graphicsMemoryMb, int systemMemoryMb, int processorCount)
        {
            if (graphicsMemoryMb < 1500 || systemMemoryMb < 7000 || processorCount <= 4)
                return global::Hecton8.Core.HectonQualityTier.Low;

            if (graphicsMemoryMb < 2200)
                return global::Hecton8.Core.HectonQualityTier.Mx350;

            if (graphicsMemoryMb < 4200)
                return global::Hecton8.Core.HectonQualityTier.Mid;

            if (graphicsMemoryMb < 8200)
                return global::Hecton8.Core.HectonQualityTier.High;

            return global::Hecton8.Core.HectonQualityTier.Ultra;
        }

        private static bool TryRunBootstrapStep(BootstrapStepToken stepToken, string phaseName, Action initializeAction)
        {
            BootstrapStatus.BeginStep(stepToken);
            try
            {
                initializeAction?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(phaseName, exception);
                return false;
            }
            finally
            {
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static bool TryRunBootstrapStep(BootstrapStepToken stepToken, string phaseName, Func<bool> initializeAction)
        {
            BootstrapStatus.BeginStep(stepToken);
            try
            {
                return initializeAction == null || initializeAction.Invoke();
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(phaseName, exception);
                return false;
            }
            finally
            {
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static void RegisterSceneLoadGuard()
        {
            if (_sceneGuardRegistered)
                return;

            SceneManager.sceneLoaded += HandleSceneLoadedGuard;
            _sceneGuardRegistered = true;
        }

        private static bool TryBuildBootstrapDependencyExecutionOrder(
            BootstrapDependencyNode[] executionOrder,
            out int executionOrderCount)
        {
            executionOrderCount = 0;
            const int nodeCount = (int)BootstrapDependencyNode.Count;
            if (executionOrder == null || executionOrder.Length < nodeCount)
                return false;

            Span<int> inDegree = stackalloc int[nodeCount];
            Span<BootstrapDependencyNode> queue = stackalloc BootstrapDependencyNode[nodeCount];

            for (int edgeIndex = 0; edgeIndex < _bootstrapDependencyEdges.Length; edgeIndex++)
            {
                BootstrapDependencyEdge edge = _bootstrapDependencyEdges[edgeIndex];
                inDegree[(int)edge.Source]++;
            }

            int queueTail = 0;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                if (inDegree[nodeIndex] == 0)
                    queue[queueTail++] = (BootstrapDependencyNode)nodeIndex;
            }

            int queueHead = 0;
            while (queueHead < queueTail)
            {
                BootstrapDependencyNode dependencyNode = queue[queueHead++];
                executionOrder[executionOrderCount++] = dependencyNode;
                for (int edgeIndex = 0; edgeIndex < _bootstrapDependencyEdges.Length; edgeIndex++)
                {
                    BootstrapDependencyEdge edge = _bootstrapDependencyEdges[edgeIndex];
                    if (edge.Dependency != dependencyNode)
                        continue;

                    int sourceIndex = (int)edge.Source;
                    inDegree[sourceIndex]--;
                    if (inDegree[sourceIndex] == 0)
                        queue[queueTail++] = edge.Source;
                }
            }

            if (executionOrderCount == nodeCount)
                return true;

            StringBuilder cycleReport = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] - bootstrap dependency-cycle fatal report - owner: GameBootstrapper
            cycleReport.AppendLine("[GameBootstrapper] Circular dependency detected in bootstrap registration graph.");

            for (int edgeIndex = 0; edgeIndex < _bootstrapDependencyEdges.Length; edgeIndex++)
            {
                BootstrapDependencyEdge edge = _bootstrapDependencyEdges[edgeIndex];
                if (inDegree[(int)edge.Source] <= 0 || inDegree[(int)edge.Dependency] <= 0)
                    continue;

                string sourceName = ResolveBootstrapDependencyNodeName(edge.Source);
                string dependencyName = ResolveBootstrapDependencyNodeName(edge.Dependency);
                cycleReport.Append(" - ");
                cycleReport.Append(sourceName);
                cycleReport.Append(" -> ");
                cycleReport.Append(dependencyName);
                cycleReport.AppendLine();
                GlobalTelemetryBus.PublishBootstrapDependencyCycle(sourceName, dependencyName);
            }

            Debug.LogError(cycleReport.ToString());
            BootstrapBiosErrorOverlay.Show("BIOS ERROR 0xBOOT_CYCLE\nACTION: SEE CONSOLE / TELEMETRY");
            return false;
        }

        private static string ResolveBootstrapDependencyNodeName(BootstrapDependencyNode node)
        {
            int index = (int)node;
            return index >= 0 && index < _bootstrapDependencyNodeNames.Length
                ? _bootstrapDependencyNodeNames[index]
                : node.ToString();
        }

        private static void HandleSceneLoadedGuard(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
                return;

            if (_isBootstrapComplete)
            {
                EnsureExtendedRegistryCoverageForActiveScene();
                SceneBootstrap sceneBootstrap = UnityEngine.Object.FindAnyObjectByType<SceneBootstrap>();
                if (sceneBootstrap != null)
                    RequestSceneActivation(sceneBootstrap);

                BootstrapBiosErrorOverlay.Hide();
                return;
            }

            TryRecoverEntryVector(scene, true);
        }

        private static bool TryRecoverEntryVector(Scene scene, bool allowRecovery)
        {
            if (IsBootstrapScene(scene))
            {
                _entryRecoveryIssued = false;
                BootstrapBiosErrorOverlay.Hide();
                return true;
            }

            string message = string.Format(
                BiosErrorMessageTemplate,
                string.IsNullOrEmpty(scene.name) ? "<unnamed>" : scene.name,
                scene.buildIndex);

            BootstrapBiosErrorOverlay.Show(message);

            if (!allowRecovery || _entryRecoveryIssued)
                return false;

            _entryRecoveryIssued = true;
            GameStartContextHolder.Reset();
            SceneManager.LoadScene(BootstrapSceneName);
            return false;
        }

        private static void EnsureExtendedRegistryCoverageForActiveScene()
        {
            TryEnsureThermodynamicsRegistryCoverage();
            TryEnsureLogisticsRegistryCoverage();
            TryEnsureWorldGenRegistryCoverage();
            TryEnsureEncounterDirectorRegistryCoverage();
            TryEnsureQuestRegistryCoverage();
        }

        private static void TryEnsureThermodynamicsRegistryCoverage()
        {
            if (GlobalRegistry.ThermodynamicsService != null)
                return;

            AbyssalThermalManager manager = UnityEngine.Object.FindAnyObjectByType<AbyssalThermalManager>();
            if (manager != null)
                GlobalRegistry.RegisterThermodynamicsRuntime(manager);
        }

        private static void TryEnsureLogisticsRegistryCoverage()
        {
            if (GlobalRegistry.Logistics != null)
                return;

            ConstructionManager manager = UnityEngine.Object.FindAnyObjectByType<ConstructionManager>();
            if (manager != null)
                GlobalRegistry.RegisterLogisticsService(manager);
        }

        private static void TryEnsureWorldGenRegistryCoverage()
        {
            if (GlobalRegistry.WorldGen != null)
                return;

            WorldProceduralScatterDirector director = UnityEngine.Object.FindAnyObjectByType<WorldProceduralScatterDirector>();
            if (director != null)
                GlobalRegistry.RegisterWorldGenService(director);
        }

        private static void TryEnsureEncounterDirectorRegistryCoverage()
        {
            if (GlobalRegistry.EncounterDirector != null)
                return;

            HectonDirectorAI director = UnityEngine.Object.FindAnyObjectByType<HectonDirectorAI>();
            if (director != null)
                GlobalRegistry.RegisterEncounterDirectorService(director);
        }

        private static void TryEnsureQuestRegistryCoverage()
        {
            if (GlobalRegistry.QuestSystem != null)
                return;

            QuestManager questManager = UnityEngine.Object.FindAnyObjectByType<QuestManager>();
            if (questManager != null)
                GlobalRegistry.RegisterQuestRuntime(questManager);
        }

        private static void EnsureBootstrapAudioListener(Scene bootstrapScene)
        {
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null || !listener.enabled || !listener.gameObject.activeInHierarchy)
                    continue;

                return;
            }

            GameObject listenerObject = new GameObject(BootstrapAudioListenerRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-only audio listener before menu handoff - owner: GameBootstrapper
            if (bootstrapScene.IsValid())
                SceneManager.MoveGameObjectToScene(listenerObject, bootstrapScene);

            listenerObject.AddComponent<AudioListener>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.audio", "created bootstrap-only listener");
#endif
        }

        private static async Awaitable WaitForJobCompletionAsync(JobHandle handle, CancellationToken ct)
        {
            int waitFrames = 0;
            try
            {
                while (!handle.IsCompleted)
                {
                    ct.ThrowIfCancellationRequested();
                    if (waitFrames >= BootstrapJobWaitWatchdogFrames)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError($"[GameBootstrapper] Job wait watchdog tripped after {waitFrames} frames. Forcing completion as cleanup barrier.");
#endif
                        break;
                    }

                    waitFrames++;
                    await Awaitable.NextFrameAsync(cancellationToken: ct);
                }
            }
            finally
            {
                handle.Complete();
            }
        }

        private static void HandleFatalBootstrapException(string phaseName, Exception exception)
        {
            if (exception == null)
                return;

            string crashMessage = BuildFatalBootstrapMessage(phaseName, exception);
            WriteFatalBootstrapLog(crashMessage);
            BootstrapBiosErrorOverlay.Show(string.Format(FatalBootOverlayMessageTemplate, phaseName));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static string BuildFatalBootstrapMessage(string phaseName, Exception exception)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("HECTON-8 FATAL BOOT CRASH").Append('\n')
                .Append("UTC: ").Append(DateTime.UtcNow.ToString("O")).Append('\n')
                .Append("PHASE: ").Append(string.IsNullOrEmpty(phaseName) ? "Unknown" : phaseName).Append('\n')
                .Append("SCENE: ").Append(string.IsNullOrEmpty(activeScene.name) ? "<unnamed>" : activeScene.name)
                .Append(" [").Append(activeScene.buildIndex).Append(']').Append('\n')
                .Append("PERSISTENT_DATA_PATH: ").Append(Application.persistentDataPath).Append('\n')
                .Append("STACKTRACE:").Append('\n')
                .Append(exception);
            return builder.ToString();
        }

        private static unsafe void WriteFatalBootstrapLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            string persistentDataPath = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentDataPath))
                return;

            string truncatedMessage = message;
            int requiredBytes = _fatalBootCrashEncoding.GetByteCount(truncatedMessage);
            while (requiredBytes > FatalBootCrashLogBufferBytes && truncatedMessage.Length > 1)
            {
                truncatedMessage = truncatedMessage.Substring(0, truncatedMessage.Length >> 1);
                requiredBytes = _fatalBootCrashEncoding.GetByteCount(truncatedMessage);
            }

            if (requiredBytes <= 0)
                return;

            string absolutePath = Path.Combine(persistentDataPath, FatalBootCrashFileName);
            NativeArray<byte> scratch = new NativeArray<byte>(requiredBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[fatal boot crash payload bytes] - bootstrap fatal log staging - owner: GameBootstrapper
            try
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                fixed (char* source = truncatedMessage)
                {
                    int bytesWritten = _fatalBootCrashEncoding.GetBytes(source, truncatedMessage.Length, destination, requiredBytes);
                    if (bytesWritten > 0)
                        AsyncWriteManager.WriteAll(absolutePath, destination, bytesWritten, out _);
                }
            }
            finally
            {
                scratch.Dispose();
            }
        }

        private static bool IsBootstrapScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.buildIndex == 0 &&
                   string.Equals(scene.name, BootstrapSceneName, System.StringComparison.Ordinal);
        }

    }

    /// <summary>
    /// Silent audio fallback used when an optional audio bootstrap owner cannot initialize.
    /// </summary>
    internal sealed class NoOpAudioService : IAudioService
    {
        // COLD ALLOC: NoOpAudioService[1] - non-critical audio fallback for deterministic bootstrap progress - owner: GameBootstrapper
        internal static readonly NoOpAudioService Instance = new NoOpAudioService();

        /// <inheritdoc />
        public bool IsInitialized => true;

        /// <inheritdoc />
        public AudioMixerGroup InterfaceGroup => null;

        /// <inheritdoc />
        public AudioMixerGroup AmbientGroup => null;

        /// <inheritdoc />
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
        }

        /// <inheritdoc />
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
        {
        }

        /// <inheritdoc />
        public void PlayStatic2D(AudioClip clip, float volume = 1f)
        {
        }

        /// <inheritdoc />
        public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
        {
        }

        /// <inheritdoc />
        public bool TryGetAcousticRadarPayload(out NativeArray<float> radialIntensityBins, out int radialResolution)
        {
            radialIntensityBins = default;
            radialResolution = 0;
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAcousticRadarGridPayload(
            out NativeArray<float> energyGrid,
            out int azimuthBins,
            out int elevationBins,
            out ComputeBuffer gridBuffer)
        {
            energyGrid = default;
            azimuthBins = 0;
            elevationBins = 0;
            gridBuffer = null;
            return false;
        }

        /// <inheritdoc />
        public void StopAll()
        {
        }
    }

    /// <summary>
    /// Data-only fauna simulation sentinel for headless boots before world fauna presentation exists.
    /// </summary>
    internal sealed class DemiurgeFaunaSimulationService : IFaunaSim
    {
        // COLD ALLOC: DemiurgeFaunaSimulationService[1] - headless data-only fauna simulation sentinel - owner: GameBootstrapper
        internal static readonly DemiurgeFaunaSimulationService Instance = new DemiurgeFaunaSimulationService();

        /// <inheritdoc />
        public bool IsReady => true;

        /// <inheritdoc />
        public int ResidentSlotCapacity => 0;
    }

    internal static class BootstrapBiosErrorOverlay
    {
        internal static void Show(string message)
        {
            HardwareErrorCanvas.Show(message);
        }

        internal static void Hide()
        {
            HardwareErrorCanvas.Hide();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class HardwareErrorCanvas : MonoBehaviour
    {
        private const string OverlayRootName = "[HardwareErrorCanvas]";
        private const int OverlaySortingOrder = 32767;

        private static HardwareErrorCanvas _instance;

        private Text _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        internal static void Show(string message)
        {
            if (GameBootstrapper.IsHeadlessBootMode || Application.isBatchMode)
            {
                // One-time critical init failure; headless cannot render the BIOS canvas.
                Debug.LogError(message);
                return;
            }

            HardwareErrorCanvas overlay = EnsureInstance();
            if (overlay == null)
                return;

            overlay.ApplyMessage(message);
        }

        internal static void Hide()
        {
            if (_instance == null)
                return;

            GameObject root = _instance.gameObject;
            _instance = null;

            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        private static HardwareErrorCanvas EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject(OverlayRootName); // COLD ALLOC: GameObject[1] - hardware-error BIOS fallback overlay root - owner: HardwareErrorCanvas
            HardwareErrorCanvas overlay = runtimeRoot.AddComponent<HardwareErrorCanvas>();
            return overlay;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            BuildVisualTree();
        }

        private void ApplyMessage(string message)
        {
            if (_messageText == null)
                BuildVisualTree();

            if (_messageText == null)
                return;

            _messageText.text = message;
        }

        private void BuildVisualTree()
        {
            if (_messageText != null)
                return;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            gameObject.AddComponent<CanvasScaler>();

            Image background = gameObject.AddComponent<Image>();
            background.color = Color.black;

            GameObject textRoot = new GameObject("Message"); // COLD ALLOC: GameObject[1] - hardware-error BIOS message node - owner: HardwareErrorCanvas
            textRoot.transform.SetParent(transform, false);

            RectTransform rectTransform = textRoot.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(72f, 72f);
            rectTransform.offsetMax = new Vector2(-72f, -72f);

            Text text = textRoot.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;

            _messageText = text;
        }
    }
}
