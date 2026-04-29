using System;
using System.IO;
using System.Text;
using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Input;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
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
        private const string FatalBootCrashFileName = "fatal_boot_crash.log";
        private const string FatalBootOverlayMessageTemplate =
            "BIOS ERROR 0xBOOT_FATAL\nPHASE: {0}\nACTION: SEE fatal_boot_crash.log";
        private const int FatalBootCrashLogBufferBytes = 24576;
        private const string BiosErrorMessageTemplate =
            "BIOS ERROR 0xBOOT\nEXPECTED: 00_BOOTSTRAP [0]\nDETECTED: {0} [{1}]\nACTION: FORCED RECOVERY";
        private static readonly UTF8Encoding _fatalBootCrashEncoding = new UTF8Encoding(false);
        private enum BootstrapDependencyNode : byte
        {
            SystemDispatcher = 0,
            GameTickManager = 1,
            SaveManager = 2,
            ObjectPoolManager = 3,
            RenderDispatcher = 4,
            SceneRuntimeService = 5,
            EquipmentInteractionHandler = 6,
            GlobalPhysicsStateManager = 7,
            PhysicsApplySystem = 8,
            DebrisManager = 9,
            EnvironmentRuntimeContextService = 10,
            OceanKinematicsRuntimeService = 11,
            SpatialAudioManager = 12,
            InputDispatcher = 13,
            PlayerRuntimeContextService = 14,
            PlayerInventoryManager = 15,
            PlayerSensoryManager = 16,
            Count = 17,
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
            "GlobalPhysicsStateManager",
            "PhysicsApplySystem",
            "DebrisManager",
            "EnvironmentRuntimeContextService",
            "OceanKinematicsRuntimeService",
            "SpatialAudioManager",
            "InputDispatcher",
            "PlayerRuntimeContextService",
            "PlayerInventoryManager",
            "PlayerSensoryManager",
        };

        private static readonly BootstrapDependencyEdge[] _bootstrapDependencyEdges =
        {
            new BootstrapDependencyEdge(BootstrapDependencyNode.GameTickManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.SaveManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.ObjectPoolManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.RenderDispatcher, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.SceneRuntimeService, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.EquipmentInteractionHandler, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.GlobalPhysicsStateManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PhysicsApplySystem, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.PhysicsApplySystem, BootstrapDependencyNode.GlobalPhysicsStateManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.DebrisManager, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.DebrisManager, BootstrapDependencyNode.ObjectPoolManager),
            new BootstrapDependencyEdge(BootstrapDependencyNode.DebrisManager, BootstrapDependencyNode.PhysicsApplySystem),
            new BootstrapDependencyEdge(BootstrapDependencyNode.EnvironmentRuntimeContextService, BootstrapDependencyNode.SystemDispatcher),
            new BootstrapDependencyEdge(BootstrapDependencyNode.OceanKinematicsRuntimeService, BootstrapDependencyNode.EnvironmentRuntimeContextService),
            new BootstrapDependencyEdge(BootstrapDependencyNode.SpatialAudioManager, BootstrapDependencyNode.SystemDispatcher),
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

        /// <summary>
        /// True once the bootstrap core finished its ordered initialization phases.
        /// </summary>
        public static bool IsBootstrapComplete => _isBootstrapComplete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isBootstrapComplete = false;
            _entryRecoveryIssued = false;

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

            TryRecoverEntryVector(SceneManager.GetActiveScene(), true);
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
        public static GameBootstrapper EnsureRuntimeInstance(GameObject owner)
        {
            if (owner == null)
                return null;

            if (!owner.TryGetComponent(out GameBootstrapper bootstrapper))
                bootstrapper = owner.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - deterministic bootstrap owner on 00_BOOTSTRAP shell - owner: BootstrapController

            return bootstrapper;
        }

        /// <summary>
        /// Executes the ordered bootstrap phases once.
        /// </summary>
        public bool InitializeBootstrap()
        {
            if (_isBootstrapComplete)
                return true;

            BootstrapStatus.BeginBoot();
            try
            {
                if (!TryRecoverEntryVector(SceneManager.GetActiveScene(), false))
                    return false;

                RegisterSceneLoadGuard();

                if (!ValidateBootstrapDependencyGraph())
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.Core, "Core", InitializeCoreLayer))
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.Environment, "Environment", InitializeEnvironmentLayer))
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.Player, "Player", InitializePlayerLayer))
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.UI, "UI", InitializeUILayer))
                    return false;

                EnsureExtendedRegistryCoverageForActiveScene();
                _isBootstrapComplete = true;
                BootstrapBiosErrorOverlay.Hide();
                return true;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException("BootstrapEntry", exception);
                return false;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void InitializeCoreLayer()
        {
            NativeArenaAllocator.Initialize();
            EnsureSystemDispatcherRegistered();
            EnsureGameTickManagerRegistered();
            EnsureSaveServiceRegistered();
            EnsureObjectPoolServiceRegistered();
            VRAMEnforcer.InitializeRuntimeBudget();
            EnsureRenderDispatcherRegistered();
            SceneInstantiationGate.EnsureRuntimeInstance();
            SceneRuntimeService sceneRuntimeService = SceneRuntimeService.EnsureRuntimeInstance();
            EquipmentInteractionHandler interactionHandler = EquipmentInteractionHandler.EnsureRuntimeInstance();
            sceneRuntimeService.InitializeService();
            interactionHandler.InitializeService();
        }

        private void InitializeEnvironmentLayer()
        {
            EnsureGlobalPhysicsStateManagerRegistered();
            PhysicsApplySystem physicsApplySystem = PhysicsApplySystem.EnsureRuntimeInstance();
            DebrisManager debrisManager = DebrisManager.EnsureRuntimeInstance();
            EnvironmentRuntimeContextService environmentContextService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
            SpatialAudioManager spatialAudioManager = EnsureAudioServiceRegistered();
            physicsApplySystem.InitializeService();
            debrisManager.InitializeService();
            environmentContextService.InitializeService();
            oceanKinematicsRuntimeService.InitializeService();
            spatialAudioManager.InitializeService();
        }

        private bool InitializePlayerLayer()
        {
            if (!InputManager.TryValidateRuntimeConfiguration(out string inputConfigurationError))
            {
                BootstrapBiosErrorOverlay.Show(inputConfigurationError);
                return false;
            }

            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                BootstrapBiosErrorOverlay.Show(
                    "BIOS ERROR 0xINPUT\nEXPECTED: Runtime InputManager instance\nDETECTED: InputManager.Instance returned null\nACTION: Repair the bootstrap input owner before boot.");
                return false;
            }

            if (!inputManager.TryValidateRuntimeActions(out string inputActionsError))
            {
                BootstrapBiosErrorOverlay.Show(inputActionsError);
                return false;
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(inputManager.gameObject);

            InputDispatcher inputDispatcher = InputDispatcher.EnsureRuntimeInstance();
            PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
            PlayerInventoryManager playerInventoryManager = PlayerInventoryManager.EnsureRuntimeInstance();
            PlayerSensoryManager playerSensoryManager = PlayerSensoryManager.EnsureRuntimeInstance();
            ContextualPhysicalIkRuntime.EnsureRuntimeInstance();
            inputDispatcher.InitializeService();
            playerContextService.InitializeService();
            playerInventoryManager.InitializeService();
            playerSensoryManager.InitializeService();
            return true;
        }

        private void InitializeUILayer()
        {
            // No UI-layer GlobalRegistry adapter exists yet.
            // Existing menu/HUD ownership remains on scene-authored controllers.
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
            if (GlobalRegistry.RenderDispatcher != null)
                return GlobalRegistry.RenderDispatcher;

            GameObject runtimeRoot = new GameObject("[RenderDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned SRP render dispatcher root - owner: GameBootstrapper
            return runtimeRoot.AddComponent<RenderDispatcher>();
        }

        private static GlobalPhysicsStateManager EnsureGlobalPhysicsStateManagerRegistered()
        {
            if (GlobalRegistry.PhysicsStateManager != null)
                return GlobalRegistry.PhysicsStateManager;

            GameObject runtimeRoot = new GameObject("[GlobalPhysicsStateManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned global physics-state manager root - owner: GameBootstrapper
            return runtimeRoot.AddComponent<GlobalPhysicsStateManager>();
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

        private static bool ValidateBootstrapDependencyGraph()
        {
            const int nodeCount = (int)BootstrapDependencyNode.Count;
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

            int processedCount = 0;
            while (processedCount < queueTail)
            {
                BootstrapDependencyNode dependencyNode = queue[processedCount++];
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

            if (processedCount == nodeCount)
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

    [DisallowMultipleComponent]
    internal sealed class BootstrapBiosErrorOverlay : MonoBehaviour
    {
        private const string OverlayRootName = "[Bootstrap BIOS ERROR]";
        private const int OverlaySortingOrder = 32767;

        private static BootstrapBiosErrorOverlay _instance;

        private Text _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        internal static void Show(string message)
        {
            BootstrapBiosErrorOverlay overlay = EnsureInstance();
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

        private static BootstrapBiosErrorOverlay EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject(OverlayRootName); // COLD ALLOC: GameObject[1] - bootstrap BIOS violation overlay root - owner: BootstrapBiosErrorOverlay
            BootstrapBiosErrorOverlay overlay = runtimeRoot.AddComponent<BootstrapBiosErrorOverlay>();
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
            gameObject.AddComponent<GraphicRaycaster>();

            Image background = gameObject.AddComponent<Image>();
            background.color = new Color(0.01f, 0.01f, 0.01f, 0.96f);

            GameObject textRoot = new GameObject("Message"); // COLD ALLOC: GameObject[1] - bootstrap BIOS overlay message node - owner: BootstrapBiosErrorOverlay
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
            text.color = new Color(1f, 0.25f, 0.25f, 1f);
            text.supportRichText = false;
            text.raycastTarget = false;

            _messageText = text;
        }
    }
}
