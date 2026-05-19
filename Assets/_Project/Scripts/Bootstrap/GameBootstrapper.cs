using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Audio.Propagation;
using Hecton8.Biolum;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Bucketing;
using Hecton8.Core.Contracts;
using Hecton8.Core.Database;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Dev;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Input;
using Hecton8.Optimization;
using Hecton8.Modding;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.UI;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CoreAudioEvent = Hecton8.Core.AudioEvent;

namespace Hecton8.Bootstrap
{
    public enum GameBootstrapperEventType : byte
    {
        GameReady = 0,
        BootstrapFailed = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GameBootstrapperEventPayload
    {
        public uint ErrorHash;
        public ushort EventType;
        public ushort Reserved;
    }

    public interface IGameBootstrapperEventListener
    {
        void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload);
    }

    /// <summary>
    /// Deterministic bootstrap owner for the GlobalRegistry core and guarded scene routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29980)]
    public sealed class GameBootstrapper : MonoBehaviour, ISlowTickable
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string FatalBootCrashFileName = "fatal_boot_crash.log";
        private const string BootStateFileName = "boot.bin";
        private const string PersistentRootName = "[PROJECT_PERSISTENT_ROOT]";
        private const string BootstrapAudioListenerRuntimeName = "[BootstrapAudioListener]";
        private const string PrefabRegistryRuntimeName = "[PrefabRegistry]";
        private const string PersistentWorldRegistryRuntimeName = "[PersistentWorldRegistry]";
        private const string RuntimePerformanceProfilerRuntimeName = "[RuntimePerformanceProfiler]";
        private const string CrashTelemetryRuntimeName = "[CrashTelemetryBuffer]";
        private const string RuntimeWatchdogRuntimeName = "[RuntimeWatchdog]";
        private const string GCMonitorRuntimeName = "[GCMonitor]";
        private const string PluginsAssemblyName = "Hecton8.Plugins";
        private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";
        private const string HectonHeadlessCommandLineArg = "-h8headless";
        private const string HeadlessCommandLineArg = "-headless";
        private const string MathLodLowKeyword = "_MATH_LOD_LOW";
        private const string MathLodHighKeyword = "_MATH_LOD_HIGH";
        private const string TierLowAddressableLabel = "Tier_Low";
        private const string TierHighAddressableLabel = "Tier_High";
        private const int OptionalServiceTimeoutMilliseconds = 5000;
        private const int ShaderWarmupTimeoutMilliseconds = 5000;
        private const int SuspiciousGraphicsMemoryFallbackThresholdMb = 256;
        private const int LowTierGraphicsMemoryMb = 3000;
        private const int LowTierProcessorCount = 6;
        private const int MidTierGraphicsMemoryMb = 4200;
        private const int HighTierGraphicsMemoryMb = 8200;
        private const int HighTierProcessorCount = 8;
        private const int UltraTierProcessorCount = 12;
        private const double ObjectPoolWarmupFrameBudgetMilliseconds = 8.0d;
        private const int FatalBootCrashLogBufferBytes = 24576;
        private const int BootStateRecordBytes = 32;
        private const uint BootStateMagic = 0x38484248u; // HBH8
        private const ushort BootStateVersion = 1;
        private const int PendingEventCapacity = 12;
        private const int SceneRootGraphLimit = 512;
        private const int WarmupBatchSize = 8;
        private const float WorldReadyPollIntervalSec = 0.1f;
        private const int WorldReadyThreshold = 100;
        private const int WorldReadyStagnationPollLimit = 40;
        private const float GroundCheckPollIntervalSec = 0.2f;
        private const float GroundCheckRayOffset = 2f;
        private const float GroundCheckRayLength = 1000f;
        private const float GroundCheckLogIntervalSec = 5f;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const int LowMemorySystemThresholdMb = 8192;
        private const int LowMemoryVramThresholdMb = 2048;
        private const int MinimalTierTargetFrameRate = 30;
        private const int DefaultTargetFrameRate = 60;
        private const int BackgroundDomainHandshakeIdle = 0;
        private const int BackgroundDomainHandshakeRunning = 1;
        private const int BackgroundDomainHandshakeComplete = 2;
        private const int BackgroundDomainHandshakeFailed = 3;
        private const int LowTierAsyncUploadBufferMb = 64;
        private const int MidTierAsyncUploadBufferMb = 128;
        private const int HighTierAsyncUploadBufferMb = 256;
        private const int LowTierAsyncUploadTimeSliceMs = 1;
        private const int MidTierAsyncUploadTimeSliceMs = 2;
        private const int HighTierAsyncUploadTimeSliceMs = 4;
        private const int HeartbeatFreezeSlowTickLimit = 3;
        private const double ServiceHeartbeatPollIntervalSeconds = 60.0d;
        private const double BootstrapSceneLoadWatchdogSeconds = 10.0d;
        private const double BootstrapJobWaitWatchdogSeconds = 10.0d;
        private const int BootstrapSceneRootScratchCapacity = 256;
        private const int BootstrapTransformScratchCapacity = 4096;
        private static readonly UTF8Encoding _fatalBootCrashEncoding = new UTF8Encoding(false);
#if UNITY_INCLUDE_TESTS
        private static readonly bool _isUnityTestRunnerProcess = ResolveUnityTestRunnerProcess();
#endif
        // COLD ALLOC: List<GameObject>[256] - bootstrap scene-root traversal scratch without scene-wide array allocation - owner: GameBootstrapper
        private static readonly List<GameObject> _bootstrapSceneRootScratch = new List<GameObject>(BootstrapSceneRootScratchCapacity);
        // COLD ALLOC: List<Transform>[4096] - bootstrap transform traversal scratch without recursive iterator allocation - owner: GameBootstrapper
        private static readonly List<Transform> _bootstrapTransformScratch = new List<Transform>(BootstrapTransformScratchCapacity);
        // COLD ALLOC: List<ProfilerRecorderHandle>[256] - reused bootstrap memory metric scanner; no per-call list allocation - owner: GameBootstrapper
        private static readonly List<ProfilerRecorderHandle> _profilerRecorderHandleScratch = new List<ProfilerRecorderHandle>(256);
        // COLD ALLOC: StringBuilder[256] - BIOS route-error overlay formatter without string.Format params/boxing - owner: GameBootstrapper
        private static readonly StringBuilder _biosErrorMessageBuilder = new StringBuilder(256);
        // COLD ALLOC: StringBuilder[128] - fatal boot overlay formatter without string.Format params array - owner: GameBootstrapper
        private static readonly StringBuilder _fatalOverlayMessageBuilder = new StringBuilder(128);
        // COLD ALLOC: StringBuilder[1024] - fatal boot crash log formatter reused across boot failures - owner: GameBootstrapper
        private static readonly StringBuilder _fatalCrashMessageBuilder = new StringBuilder(1024);
        // COLD ALLOC: RegistryBucket<IGameBootstrapperEventListener>[12] - bootstrap listeners drained on dispatcher LateUpdate - owner: GameBootstrapper
        private static readonly RegistryBucket<IGameBootstrapperEventListener> _listeners =
            new RegistryBucket<IGameBootstrapperEventListener>(PendingEventCapacity);
        // COLD ALLOC: Dictionary<uint,string>[8] - hashed bootstrap failure reasons for cold-path diagnostics resolution - owner: GameBootstrapper
        private static readonly Dictionary<uint, string> _failureReasonsByHash = new Dictionary<uint, string>(8);
        private static GlobalDataVault _globalDataVault;
        private static H8MacroDatabaseService _macroDatabaseService;
        private static BurstTokenBucketJobAdmissionService _jobAdmissionService;
        private static JobAdmissionTelemetryBridge _jobAdmissionTelemetryBridge;
        private static ModuloSimulationBucketer _simulationBucketerService;
        private static NativeQueue<GameBootstrapperEventPayload> _pendingEvents;
        private static NativeQueue<GameBootstrapperEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static bool _h8MemoryFatalLogHooked;
        private static bool _h8MemoryFatalDumpWritten;
#if UNITY_EDITOR
        private static string _pendingDirtySceneReloadPath;
        private static readonly List<GameObject> _dontDestroyRootScratch = new List<GameObject>(32); // COLD ALLOC: List<GameObject>[32] - editor-only DDOL residue scan scratch - owner: GameBootstrapper
#endif
        private static readonly string[] _TextureMemoryCandidates =
        {
            "Texture Memory",
            "Texture Used Memory"
        };
        private static readonly string[] _TotalReservedMemoryCandidates =
        {
            "Total Reserved Memory",
            "System Used Memory",
            "Total Used Memory"
        };

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

        private enum BootStateMarker : byte
        {
            Unknown = 0,
            Started = 1,
            PhaseStarted = 2,
            ServiceStarted = 3,
            CoreReady = 4,
            WorldGen = 5,
            Complete = 6,
            Fatal = 7,
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
            NativeInputManager = 16,
            InputDispatcher = 17,
            PlayerRuntimeContextService = 18,
            PlayerInventoryManager = 19,
            PlayerSensoryManager = 20,
            PowerGridManager = 21,
            ConstructionManager = 22,
            ConnectionSplineBatchRenderer = 23,
            BeaconNetworkSystem = 24,
            ModWorldPersistenceManager = 25,
            Count = 26,
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
            "NativeInputManager",
            "InputDispatcher",
            "PlayerRuntimeContextService",
            "PlayerInventoryManager",
            "PlayerSensoryManager",
            "PowerGridManager",
            "ConstructionManager",
            "ConnectionSplineBatchRenderer",
            "BeaconNetworkSystem",
            "ModWorldPersistenceManager",
        };

        private static readonly object _bootstrapDependencyScratchLock = new object();
        // COLD ALLOC: object[1] - bootstrap reflection argument scratch for isolated optional services - owner: GameBootstrapper
        private static readonly object[] _bootstrapReflectionSingleArgumentScratch = new object[1];
        // COLD ALLOC: GlobalRegistryServiceSlot[bootstrap-node-count] - registry dependency execution order scratch - owner: GameBootstrapper
        private static readonly GlobalRegistryServiceSlot[] _bootstrapRegistryExecutionOrderScratch =
            new GlobalRegistryServiceSlot[(int)BootstrapDependencyNode.Count];
        private static readonly uint _BootstrapTotalBootTimeHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.TotalBootTimeMs"));
        private static readonly uint _GameBootstrapperContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("GameBootstrapper"));
        private static readonly uint _ServiceHeartbeatFreezeHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SERVICE_HEARTBEAT_FREEZE"));
        private static bool _isBootstrapComplete;
        private static bool _sceneGuardRegistered;
        private static bool _entryRecoveryIssued;
        private static BootstrapPhase _currentPhase;
        private static InputManager _bootstrapInputManager;
        private static bool _headlessBootMode;
        private static bool _preWarmAssetsReady;
        private static bool _bootstrapDurationTelemetryPublished;
        private static long _bootstrapStartTimestamp;
        private static uint _registryCoreReadyChecksum;
        private static bool _bootStateSafeModeRequested;

        [Header("Bootstrap Prewarm")]
        [Tooltip("Shader variant collections warmed during MemoryPreWarm before scene or player activation.")]
        [SerializeField] private ShaderVariantCollection[] shaderVariantCollections;
        [Tooltip("Analytical caustics compute shader injected into the caustics service during visual runtime bootstrap.")]
        [SerializeField] private ComputeShader analyticalCausticsCompute;
        [Tooltip("Optional human-authored GlobalDataVault sizing facade. If absent, legacy binary archaeology or mock config drives the vault.")]
        [SerializeField] private VaultConfigurationAsset vaultConfigurationAsset;
        [SerializeField] private uint expectedBiosRegistryFnv1a;
#if UNITY_ADDRESSABLES_EXIST
        [Tooltip("Addressable groups loaded sequentially before dependent services consume them.")]
        [SerializeField] private AssetLabelReference[] addressableDependencyGroups;
#endif

        [Header("Scene Activation")]
        [Tooltip("If true, always start a new game and ignore the handoff context.")]
        [SerializeField] private bool forceNewGame;
        [SerializeField] private bool prewarmProceduralScatterBeforePlayerActivation = true;
        [SerializeField, Range(1, 4)] private int scatterBootstrapPrimePasses = 2;
        [SerializeField] private List<WarmupEntry> warmupEntries = new List<WarmupEntry>();
        [SerializeField] private MonoBehaviour playerSpawner;
        [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(0f, 10f, 0f);
        [SerializeField] private GameObject playerObject;
        [SerializeField] private MonoBehaviour playerController;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private float worldGenWaitTime = 2f;
        [SerializeField] private float bootstrapTimeout = 30f;
        [SerializeField] private float groundReadyTimeout = 15f;
        [SerializeField] private LayerMask groundReadyLayerMask = HectonLayerMasks.SeamProbeLayerMask;
        [SerializeField] private bool verboseSceneActivationLogging = true;
#pragma warning disable CS0414
        [SerializeField] private string _debugSceneActivationStep = "Not started";
        [SerializeField] private bool _debugSceneActivationCompleted;
        [SerializeField] private float _debugStartupTextureMemoryMb;
        [SerializeField] private float _debugStartupReservedMemoryMb;
        [SerializeField] private string _debugStartupTextureMetric = "Unresolved";
        [SerializeField] private string _debugStartupReservedMetric = "Unresolved";
#pragma warning restore CS0414

#if UNITY_ADDRESSABLES_EXIST
        [Header("Bootstrap UI")]
        [Tooltip("Addressable HUD/PDA prefabs that must instantiate before UI bootstrap can complete.")]
        [SerializeField] private AssetReferenceGameObject[] uiAddressablePrefabs;
#endif

        private bool _bootstrapRunInProgress;
        private bool _sceneActivationRunInProgress;
        private bool _sceneActivationRequested;
        private bool _sceneActivationStarted;
        private ulong _sceneActivationSceneHandle = ulong.MaxValue;
        private bool _isLoadingSave;
        private bool _slowTickableRegistered;
        private double _nextServiceHeartbeatPollTime;
        private WorldProceduralScatterDirector _worldProceduralScatterDirector;
        private int _backgroundDomainHandshakeState;
        private string _backgroundDomainHandshakePath;
        private string _backgroundDomainHandshakeError;
        private readonly RaycastHit[] _groundCheckHits = new RaycastHit[1]; // COLD ALLOC: bootstrap ground-ready probe only needs nearest collider.
        private readonly List<GameObject> _shippingCleanupRootObjects = new List<GameObject>(64); // COLD ALLOC: List<GameObject>[64] - root cache for one-shot shipping scene cleanup - owner: GameBootstrapper
        private readonly List<Transform> _shippingCleanupTraversalStack = new List<Transform>(256); // COLD ALLOC: List<Transform>[256] - traversal stack for one-shot shipping scene cleanup - owner: GameBootstrapper
#if UNITY_ADDRESSABLES_EXIST
        private AsyncOperationHandle<GameObject>[] _uiPrefabInstanceHandles;
#endif
        // COLD ALLOC: BootstrapDependencyNode[bootstrap-node-count] - cached Kahn topological service execution order - owner: GameBootstrapper
        private readonly BootstrapDependencyNode[] _bootstrapExecutionOrder = new BootstrapDependencyNode[(int)BootstrapDependencyNode.Count];
        private readonly int[] _heartbeatTickSamples = new int[(int)BootstrapDependencyNode.Count];
        private readonly byte[] _heartbeatFrozenSamples = new byte[(int)BootstrapDependencyNode.Count];
        private int _bootstrapExecutionOrderCount;

        [Serializable]
        public struct WarmupEntry
        {
            public GameObject prefab;
            [Min(1)]
            public int count;
            public string label;
        }

        /// <summary>
        /// True once the bootstrap core finished its ordered initialization phases.
        /// </summary>
        public static bool IsBootstrapComplete => _isBootstrapComplete;

        public static bool IsGameReady => BootstrapState.IsGameReady;

        public static bool HasActiveInstance => BootstrapState.HasActiveInstance;

        public static GameBootstrapper ActiveInstance => GlobalRegistry.BootstrapperRuntime;

        public static GameObject CurrentPlayerObject => BootstrapState.CurrentPlayerObject;

        public static Transform CurrentPlayerTransform => BootstrapState.CurrentPlayerTransform;

        public static int PendingEventCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// True when boot is running in data-only server/testing mode.
        /// </summary>
        public static bool IsHeadlessBootMode => _headlessBootMode;

        /// <summary>
        /// True once bootstrap shader and residency prewarm gates have completed.
        /// </summary>
        public static bool ArePreWarmAssetsReady => _preWarmAssetsReady;

        internal static bool HasRuntimeInstance => ActiveInstance != null;

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

        public static bool TryValidateSceneRootBudget(string sceneName, string context)
        {
            if (string.IsNullOrEmpty(sceneName))
                return true;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return TryValidateSceneRootBudget(scene, context);
        }

        public static bool TryValidateSceneRootBudget(Scene scene, string context)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return true;

            int rootCount = scene.rootCount;
            if (rootCount <= SceneRootGraphLimit)
                return true;

            Debug.LogError(
                "[GameBootstrapper] SCENE_GRAPH_CORRUPTION_GUARD abort. context=" +
                context +
                " scene=" +
                scene.name +
                " rootCount=" +
                rootCount +
                " limit=" +
                SceneRootGraphLimit);
            return false;
        }

        public static bool TryGetCurrentPlayerTransform(out Transform playerTransform)
        {
            return BootstrapState.TryGetCurrentPlayerTransform(out playerTransform);
        }

        public static bool RegisterBiolumDirector(HectonBiolumManager director)
        {
            if (!Application.isPlaying || director == null)
                return false;

            EnsureRuntimeInstance();
            PersistRuntimeService(director);

            HectonBiolumManager registered = GlobalRegistry.BiolumManager;
            if (registered != null && !ReferenceEquals(registered, director))
                return false;

            GlobalRegistry.RegisterBiolumManagerRuntime(director);
            return ReferenceEquals(GlobalRegistry.BiolumManager, director);
        }

        public static void UnregisterBiolumDirector(HectonBiolumManager director)
        {
            if (director == null)
                return;

            GlobalRegistry.UnregisterBiolumManagerRuntime(director);
        }

        public static void Register(IGameBootstrapperEventListener listener)
        {
            if (listener == null)
                return;

            EnsureEventQueueInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IGameBootstrapperEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPendingEvents()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainPendingEventsWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out GameBootstrapperEventPayload payload))
                    return;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
                scanBudget--;

                IGameBootstrapperEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IGameBootstrapperEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnGameBootstrapperEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static bool TryResolveBootstrapFailureReason(uint errorHash, out string reason)
        {
            return _failureReasonsByHash.TryGetValue(errorHash, out reason);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ResetBootstrapEventState();
            GlobalRegistry.ClearBootstrapperRuntime(null);
            _isBootstrapComplete = false;
            _entryRecoveryIssued = false;
            _currentPhase = BootstrapPhase.HardwareCheck;
            _bootstrapInputManager = null;
            _headlessBootMode = false;
            _preWarmAssetsReady = false;
            _bootstrapDurationTelemetryPublished = false;
            _bootstrapStartTimestamp = 0L;
            _registryCoreReadyChecksum = 0u;
            _bootStateSafeModeRequested = false;
            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            _biosErrorMessageBuilder.Length = 0;
            _fatalOverlayMessageBuilder.Length = 0;
            BootstrapState.Reset();
            RemoveH8MemoryFatalDumpHook();
            ShutdownGlobalDataVaultForBootstrapTeardown();
            H8Memory.Shutdown();
            if (_sceneGuardRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoadedGuard;
                _sceneGuardRegistered = false;
            }

            BootstrapBiosErrorOverlay.Hide();
        }

        private static void ResetBootstrapEventState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(GameBootstrapper), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(GameBootstrapper), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _failureReasonsByHash.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        private static void EnsureEventQueueInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<GameBootstrapperEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<GameBootstrapperEventPayload>[12] - deferred bootstrap event lane flushed by SystemDispatcher LateUpdate - owner: GameBootstrapper
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(GameBootstrapper),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<GameBootstrapperEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<GameBootstrapperEventPayload>[12] - next-frame bootstrap event lane prevents same-frame reentrant dispatch - owner: GameBootstrapper
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(GameBootstrapper),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void RaiseGameReadyEvent()
        {
            EnsureEventQueueInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            GameBootstrapperEventPayload payload = new GameBootstrapperEventPayload
            {
                ErrorHash = 0u,
                EventType = (ushort)GameBootstrapperEventType.GameReady,
                Reserved = 0
            };

            EnqueueBootstrapEvent(in payload);
        }

        private static void RaiseBootstrapFailedEvent(string error)
        {
            uint errorHash = string.IsNullOrWhiteSpace(error)
                ? 0u
                : unchecked((uint)Hecton.Localization.LocHash.Compute(error));
            EnsureEventQueueInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (errorHash != 0u && !_failureReasonsByHash.ContainsKey(errorHash))
                _failureReasonsByHash.Add(errorHash, error);

            GameBootstrapperEventPayload payload = new GameBootstrapperEventPayload
            {
                ErrorHash = errorHash,
                EventType = (ushort)GameBootstrapperEventType.BootstrapFailed,
                Reserved = 0
            };

            EnqueueBootstrapEvent(in payload);
        }

        private static void EnqueueBootstrapEvent(in GameBootstrapperEventPayload payload)
        {
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void DrainPendingEventsWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<GameBootstrapperEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget > 0 && !queue.IsEmpty())
            {
                if (!queue.TryDequeue(out _))
                    return false;

                if (pendingCount > 0)
                    pendingCount--;
                scanBudget--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<GameBootstrapperEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void GuardInitialSceneEntry()
        {
#if UNITY_INCLUDE_TESTS
            if (_isUnityTestRunnerProcess)
                return;
#endif
            if (!Application.isPlaying || _isBootstrapComplete)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (TryRecoverEntryVector(activeScene, true) && IsBootstrapScene(activeScene))
                EnsureRuntimeInstance()?.BeginBootstrap();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void GuardEntryVectorBeforeSceneLoad()
        {
#if UNITY_INCLUDE_TESTS
            if (_isUnityTestRunnerProcess)
                return;
#endif
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
            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper != null)
                return bootstrapper;

            if (TryResolveBootstrapControllerOwner(out GameObject bootstrapOwner))
                return EnsureRuntimeInstance(bootstrapOwner);

            Scene activeScene = SceneManager.GetActiveScene();
            GameObject runtimeRoot = new GameObject(PersistentRootName); // COLD ALLOC: GameObject[1] - bootstrap authority root when scene authoring omitted it - owner: GameBootstrapper
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
            GameBootstrapper runtimeBootstrapper = ActiveInstance;
            if (runtimeBootstrapper != null)
            {
                TryAdoptBootstrapControllerCausticsCompute(runtimeBootstrapper, owner);
                return runtimeBootstrapper;
            }

            if (owner == null)
                return EnsureRuntimeInstance();

            if (!owner.TryGetComponent(out GameBootstrapper bootstrapper))
                bootstrapper = owner.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - deterministic bootstrap owner on 00_BOOTSTRAP shell - owner: BootstrapController

            TryAdoptBootstrapControllerCausticsCompute(bootstrapper, owner);
            return bootstrapper;
        }

        private static void TryAdoptBootstrapControllerCausticsCompute(GameBootstrapper bootstrapper, GameObject owner)
        {
            if (bootstrapper == null || bootstrapper.analyticalCausticsCompute != null || owner == null)
                return;

            if (!owner.TryGetComponent(out BootstrapController bootstrapController))
                return;

            ComputeShader computeShader = bootstrapController.AnalyticalCausticsCompute;
            if (computeShader != null)
                bootstrapper.analyticalCausticsCompute = computeShader;
        }

        private static bool TryResolveBootstrapControllerOwner(out GameObject owner)
        {
            owner = null;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!Application.isPlaying || !IsBootstrapScene(activeScene))
                return false;

            if (!TryResolveSceneComponent(activeScene, includeInactive: false, out BootstrapController bootstrapController) ||
                bootstrapController == null ||
                bootstrapController.gameObject.scene != activeScene)
                return false;

            owner = bootstrapController.gameObject;
            return owner != null;
        }

        private static bool TryResolveSceneComponent<T>(
            Scene scene,
            bool includeInactive,
            out T component)
            where T : Component
        {
            component = null;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null)
                    continue;

                if (!includeInactive && !root.activeInHierarchy)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if ((includeInactive || currentObject.activeInHierarchy) &&
                    currentObject.TryGetComponent(out component) &&
                    component != null)
                {
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return true;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            component = null;
            return false;
        }

        private static bool TryResolveSceneTaggedObject(
            Scene scene,
            string tag,
            out GameObject taggedObject)
        {
            taggedObject = null;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(tag))
                return false;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || !root.activeInHierarchy)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if (currentObject.activeInHierarchy && currentObject.CompareTag(tag))
                {
                    taggedObject = currentObject;
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return true;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            return false;
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
            GameBootstrapper runtimeBootstrapper = ActiveInstance;
            if (runtimeBootstrapper != null && runtimeBootstrapper != this)
            {
                Destroy(this);
                return;
            }

            GlobalRegistry.BeginRegistration();
            GlobalRegistry.RegisterBootstrapperRuntime(this);

            if (Application.isPlaying)
            {
                gameObject.name = PersistentRootName;
                if (transform.parent != null)
                    transform.SetParent(null, true);

                MarkProjectPersistentRoot();
                EnforceProjectPersistentRoot();
            }
        }

        private void OnEnable()
        {
            _nextServiceHeartbeatPollTime = 0d;
            TryRegisterBootstrapSlowTickable();
        }

        private void OnDisable()
        {
            if (!_slowTickableRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _slowTickableRegistered = false;
        }

        private void Start()
        {
            Scene activeScene = gameObject.scene;
            if (Application.isPlaying && IsBootstrapScene(activeScene))
                BeginBootstrap();
        }

        private void OnDestroy()
        {
#if UNITY_ADDRESSABLES_EXIST
            ReleaseAddressableUIPrefabs();
#endif
            ShutdownServicesInReverseBootstrapOrder();
            BootstrapState.ClearCurrentPlayerObject(playerObject);
            BootstrapState.PublishBootstrapPresence(false);
            if (Application.isPlaying)
                BootstrapState.PublishGameReady(false);
            DisposeSessionNativeStateForShutdown();
            GlobalRegistry.ClearBootstrapperRuntime(this);
        }

        public void SlowTick()
        {
            if (!_isBootstrapComplete || _bootstrapExecutionOrderCount <= 0)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextServiceHeartbeatPollTime)
                return;

            _nextServiceHeartbeatPollTime = now + ServiceHeartbeatPollIntervalSeconds;
            for (int index = 0; index < _bootstrapExecutionOrderCount; index++)
            {
                BootstrapDependencyNode node = _bootstrapExecutionOrder[index];
                object service = ResolveBootstrapDependencyService(node);
                IServiceHeartbeat heartbeat = service as IServiceHeartbeat;
                ISystem system = service as ISystem;
                if (heartbeat == null && system == null)
                    continue;

                if (heartbeat != null &&
                    (!heartbeat.IsServiceReady ||
                     heartbeat.HeartbeatState == ServiceHeartbeatState.Failed ||
                     heartbeat.HeartbeatState == ServiceHeartbeatState.Shutdown))
                {
                    continue;
                }

                int tickCount = system != null ? system.TickCount : heartbeat.TickCount;
                if (tickCount != _heartbeatTickSamples[index])
                {
                    _heartbeatTickSamples[index] = tickCount;
                    _heartbeatFrozenSamples[index] = 0;
                    continue;
                }

                if (_heartbeatFrozenSamples[index] < byte.MaxValue)
                    _heartbeatFrozenSamples[index]++;

                if (_heartbeatFrozenSamples[index] == HeartbeatFreezeSlowTickLimit)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        "[GameBootstrapper] SERVICE_HEARTBEAT_FREEZE service=" +
                        ResolveBootstrapDependencyNodeName(node) +
                        " tick=" +
                        tickCount);
#endif
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _ServiceHeartbeatFreezeHash,
                        _GameBootstrapperContextHash,
                        tickCount);
                    CrashTelemetryBuffer.ReportRuntimeWatchdogStall((uint)index, unchecked((uint)tickCount));
                }
            }
        }

        private void TryRegisterBootstrapSlowTickable()
        {
            if (!Application.isPlaying || _slowTickableRegistered || GlobalRegistry.Dispatcher == null)
                return;

            _slowTickableRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private static void DisposeSessionNativeStateForShutdown()
        {
            ShutdownSystemDispatcherForBootstrapTeardown();
            ShutdownSimulationBucketerServiceForBootstrapTeardown();
            ShutdownJobAdmissionServiceForBootstrapTeardown();
            Hecton8.Modding.ModLoader.ResetStaticState();
            Hecton8.Modding.ModRegistryEvents.ResetStaticState();
            BootstrapEvents.ResetStaticState();
            ResetBootstrapEventState();
            SaveEvents.ResetStaticState();
            Hecton.Localization.LocalizationEvents.ResetStaticState();
            ObjectPoolDiagnostics.ResetStaticState();
            UIStateStore.Shutdown();
            HighPressureEvents.Shutdown();
            FatalPressureImplosionEvents.Shutdown();
            AcousticZoneEvents.ResetStaticState();
            BaseAirlockEvents.ResetStaticState();
            Hecton8.Interaction.InteractionEvents.ResetStaticState();
            Hecton8.Crafting.CraftingEvents.ResetStaticState();
            Hecton8.Power.PowerGridTelemetryEvents.ResetStaticState();
            PhysicsEventBus.Shutdown();
            FrameTimeWatchdog.Shutdown();
            MathGuard.Dispose();
            GlobalSignals.DisposeAllQueues();
            LogisticsPipeTransportScheduler.Shutdown();
            WorldSpatialHashGrid.ClearRuntimeState();
            global::Hecton8.Data.H8StaticDataArena.Shutdown();
            PreInitAssetIdMap.Shutdown();
            NativeArenaAllocator.Shutdown();
            ShutdownGlobalDataVaultForBootstrapTeardown();
            H8Memory.Shutdown();
            GlobalRegistry.DisposeServiceReboundQueuesForShutdown();
        }

        private static void ShutdownSystemDispatcherForBootstrapTeardown()
        {
            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher == null)
                dispatcher = SystemDispatcher.ActiveRuntimeInstance;

            if (dispatcher != null)
            {
                dispatcher.OnServiceShutdown();
                return;
            }

            SystemDispatcher.ClearAllLanes();
        }

        private static void ShutdownJobAdmissionServiceForBootstrapTeardown()
        {
            IJobAdmissionService service = GlobalRegistry.JobAdmission;
            if (service != null)
            {
                JobAdmissionSchedulerBridge.ClearService(service);
                GlobalRegistry.UnregisterJobAdmissionRuntime(service);
                service.Dispose();
            }

            _jobAdmissionService = null;
            _jobAdmissionTelemetryBridge = null;
        }

        private static void ShutdownSimulationBucketerServiceForBootstrapTeardown()
        {
            ISimulationBucketer service = GlobalRegistry.SimulationBucketer;
            if (service != null && ReferenceEquals(service, _simulationBucketerService))
            {
                GlobalRegistry.UnregisterSimulationBucketerRuntime(service);
                service.Dispose();
            }

            _simulationBucketerService = null;
        }

        private static void ShutdownGlobalDataVaultForBootstrapTeardown()
        {
            ShutdownMacroDatabaseForBootstrapTeardown();

            if (_globalDataVault == null)
                return;

            if (ReferenceEquals(GlobalRegistry.DataVault, _globalDataVault))
                GlobalRegistry.UnregisterDataVault(_globalDataVault);

            _globalDataVault.Dispose();
            _globalDataVault = null;
        }

        private static void ShutdownMacroDatabaseForBootstrapTeardown()
        {
            if (_macroDatabaseService == null)
                return;

            if (ReferenceEquals(GlobalRegistry.MacroDatabase, _macroDatabaseService))
                GlobalRegistry.UnregisterMacroDatabase(_macroDatabaseService);

            _macroDatabaseService.Shutdown();
            _macroDatabaseService = null;
        }

        /// <summary>
        /// Starts the unified Awaitable bootstrap state machine if it has not already run.
        /// </summary>
        public void BeginBootstrap()
        {
            if (_isBootstrapComplete || _bootstrapRunInProgress)
                return;

            GlobalRegistry.BeginRegistration();
            EnsureCrashTelemetryBufferRegistered();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimeWatchdogRegistered();
            EnsureGCMonitorRegistered();
#endif
            _bootstrapRunInProgress = true;
            _ = RunBootstrapStateMachineAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Requests gameplay scene activation through the unified bootstrap owner.
        /// </summary>
        public static void RequestSceneActivation()
        {
            GameBootstrapper bootstrapper = EnsureRuntimeInstance();
            if (bootstrapper == null)
                return;

            bootstrapper.ScheduleSceneActivation();
        }

        public static void RequestSceneActivation(MonoBehaviour ignoredOwner)
        {
            RequestSceneActivation();
        }

        private void ScheduleSceneActivation()
        {
            _sceneActivationRequested = true;
            Scene activeScene = SceneManager.GetActiveScene();
            ulong activeSceneHandle = activeScene.handle.GetRawData();
            if (_sceneActivationSceneHandle != activeSceneHandle)
            {
                _sceneActivationSceneHandle = activeSceneHandle;
                _sceneActivationStarted = false;
                _debugSceneActivationCompleted = false;
            }

            if (!_isBootstrapComplete || _sceneActivationRunInProgress)
                return;

            _sceneActivationRunInProgress = true;
            GlobalRegistry.BeginRegistration();
            _ = RunSceneActivationAsync(destroyCancellationToken);
        }

        private async Awaitable<bool> RunBootstrapStateMachineAsync(CancellationToken ownerToken)
        {
            BootstrapStatus.BeginBoot();
            WriteBootStateRecord(BootStateMarker.Started, BootstrapPhase.HardwareCheck, GlobalRegistryServiceSlot.Unknown);
            _bootstrapStartTimestamp = Stopwatch.GetTimestamp();
            _bootstrapDurationTelemetryPublished = false;
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
                _registryCoreReadyChecksum = CalculateRegistryActiveServiceTypeHash();
                if (expectedBiosRegistryFnv1a != 0u && _registryCoreReadyChecksum != expectedBiosRegistryFnv1a)
                {
                    HandleFatalBootstrapException(
                        "BIOSIntegrityChecksum",
                        new InvalidOperationException("[GameBootstrapper] BIOS integrity checksum mismatch."));
                    return false;
                }

                WriteBootStateRecord(BootStateMarker.CoreReady, BootstrapPhase.CoreServices, GlobalRegistryServiceSlot.Unknown);
                BootstrapBiosErrorOverlay.Hide();
                DisableGarbageCollectorAfterCoreReady();
                BootstrapEvents.NotifyBootstrapComplete();

                if (!await RunBootstrapPhaseAsync(BootstrapPhase.SceneActivate, BootstrapStepToken.SceneActivate, InitializeSceneActivatePhaseAsync, ct))
                    return false;

                GlobalRegistry.LockReady();
                PublishTotalBootTimeTelemetry();
                _currentPhase = BootstrapPhase.Complete;
                WriteBootStateRecord(BootStateMarker.Complete, BootstrapPhase.Complete, GlobalRegistryServiceSlot.Unknown);
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
                WriteBootStateRecord(BootStateMarker.Fatal, BootstrapPhase.Fatal, GlobalRegistryServiceSlot.Unknown);
                HandleFatalBootstrapException("BootstrapEntry", exception);
                return false;
            }
            finally
            {
                _bootstrapRunInProgress = false;
            }
        }

        private static void PublishTotalBootTimeTelemetry()
        {
            if (_bootstrapDurationTelemetryPublished || _bootstrapStartTimestamp <= 0L)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - _bootstrapStartTimestamp;
            float elapsedMilliseconds = (float)(elapsedTicks * 1000d / Stopwatch.Frequency);
            GlobalTelemetryBus.PublishBootstrapDuration(
                _BootstrapTotalBootTimeHash,
                _GameBootstrapperContextHash,
                elapsedMilliseconds);
            _bootstrapDurationTelemetryPublished = true;
        }

        private async Awaitable<bool> RunBootstrapPhaseAsync(
            BootstrapPhase phase,
            BootstrapStepToken stepToken,
            Func<CancellationToken, Awaitable<bool>> phaseAction,
            CancellationToken ct)
        {
            _currentPhase = phase;
            WriteBootStateRecord(BootStateMarker.PhaseStarted, phase, GlobalRegistryServiceSlot.Unknown);
            BootstrapStatus.BeginStep(stepToken);
            long phaseStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                bool phaseComplete = phaseAction == null || await phaseAction(ct);
                if (!phaseComplete)
                {
                    LogBootstrapPhaseFailure(phase);
                    _currentPhase = BootstrapPhase.Fatal;
                }

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
                HandleFatalBootstrapException(ResolveBootstrapPhaseName(phase), exception);
                return false;
            }
            finally
            {
                double elapsedMilliseconds =
                    (Stopwatch.GetTimestamp() - phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
                CrashTelemetryBuffer.RecordBootstrapPhaseDuration(stepToken, elapsedMilliseconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                BootstrapHealthMonitor.RecordPhaseDuration(stepToken, elapsedMilliseconds);
#endif
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static string ResolveBootstrapPhaseName(BootstrapPhase phase)
        {
            switch (phase)
            {
                case BootstrapPhase.HardwareCheck:
                    return nameof(BootstrapPhase.HardwareCheck);
                case BootstrapPhase.MemoryPreWarm:
                    return nameof(BootstrapPhase.MemoryPreWarm);
                case BootstrapPhase.CoreServices:
                    return nameof(BootstrapPhase.CoreServices);
                case BootstrapPhase.Environment:
                    return nameof(BootstrapPhase.Environment);
                case BootstrapPhase.Player:
                    return nameof(BootstrapPhase.Player);
                case BootstrapPhase.UI:
                    return nameof(BootstrapPhase.UI);
                case BootstrapPhase.SceneActivate:
                    return nameof(BootstrapPhase.SceneActivate);
                case BootstrapPhase.Complete:
                    return nameof(BootstrapPhase.Complete);
                case BootstrapPhase.Fatal:
                    return nameof(BootstrapPhase.Fatal);
                default:
                    return "Unknown";
            }
        }

        private async Awaitable<bool> InitializeHardwareCheckPhaseAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                _headlessBootMode = IsHeadlessBootRequested();
                InspectPreviousBootState();
                global::Hecton8.Core.HectonHardwareProfile hardwareProfile = CaptureHardwareProfile();
                GlobalRegistry.RegisterHardwareProfile(in hardwareProfile);
                ApplyMemoryGate(in hardwareProfile);
                ApplyScalabilityMatrix(in hardwareProfile);
                ValidateOceanKinematicsPluginContract();
                StartBackgroundDomainHandshake();

                Scene activeScene = SceneManager.GetActiveScene();
                if (!TryRecoverEntryVector(activeScene, false))
                    return false;

                RegisterSceneLoadGuard();
                if (!_headlessBootMode)
                    EnsureBootstrapAudioListener(activeScene);

                bool dependencyGraphValid = TryBuildBootstrapDependencyExecutionOrder(
                    _bootstrapExecutionOrder,
                    out _bootstrapExecutionOrderCount);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return dependencyGraphValid;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeMemoryPreWarmPhaseAsync(CancellationToken ct)
        {
            try
            {
                _preWarmAssetsReady = false;
                InitializeBootstrapAllocators();
                InitializeBootstrapEventBuses();
                BinaryLayoutManifest.VerifyColdBoot();
                InitializeBootstrapMmfStorage();
                uint appVersionHash = global::Hecton8.Data.H8DataHash.ComputeFnv1A32(Application.version.AsSpan());
                if (!InitializeBootstrapDataMonolith(appVersionHash))
                {
                    Debug.LogError("[GameBootstrapper] Data Monolith boot validation failed.");
                    return false;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializePresentationBootstrapAsync(CancellationToken ct)
        {
            try
            {
                if (_headlessBootMode)
                {
                    _preWarmAssetsReady = true;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                    return true;
                }

                if (!_headlessBootMode)
                {
                    WarmMathLodShaderKeywords();
                    VRAMEnforcer.InitializeRuntimeBudget();
                    VRAMOptimizationBootstrap.EnsureRuntimeManagers();
                    SceneInstantiationGate gate = SceneInstantiationGate.EnsureRuntimeInstance();
                    PersistRuntimeService(gate);
                }

#if UNITY_ADDRESSABLES_EXIST
                if (!_headlessBootMode && !await PreWarmTierAddressableTextureGroupAsync(ct))
                    return false;
#endif

                if (!await WarmConfiguredShaderVariantCollectionsAsync(ct))
                    return false;

                _preWarmAssetsReady = true;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private void InitializeBootstrapAllocators()
        {
            byte scalabilityProfile = GlobalRegistry.ScalabilityTierProfileByte;
            VaultMemoryLayoutConfig memoryLayoutConfig = vaultConfigurationAsset != null
                ? vaultConfigurationAsset.BuildRuntimeConfig(scalabilityProfile)
                : VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            long vaultArenaLimitBytes = memoryLayoutConfig.ArenaLimitBytes > 0L
                ? memoryLayoutConfig.ArenaLimitBytes
                : GlobalDataVault.ResolveArenaCapacityLimit(scalabilityProfile);
            H8Memory.Initialize(poolCapBytes: vaultArenaLimitBytes);
            H8Memory.ConfigurePoolCap(vaultArenaLimitBytes);
            InstallH8MemoryFatalDumpHook();
            EnsureGlobalDataVaultRegistered(vaultArenaLimitBytes, memoryLayoutConfig.BufferCapacity, in memoryLayoutConfig, vaultConfigurationAsset != null);
            NativeArenaAllocator.Initialize();
        }

        private static void InstallH8MemoryFatalDumpHook()
        {
            Application.logMessageReceived -= HandleH8MemoryFatalLog;
            Application.logMessageReceived += HandleH8MemoryFatalLog;
            _h8MemoryFatalLogHooked = true;
            _h8MemoryFatalDumpWritten = false;
        }

        private static void RemoveH8MemoryFatalDumpHook()
        {
            if (!_h8MemoryFatalLogHooked)
                return;

            Application.logMessageReceived -= HandleH8MemoryFatalLog;
            _h8MemoryFatalLogHooked = false;
            _h8MemoryFatalDumpWritten = false;
        }

        private static void HandleH8MemoryFatalLog(string condition, string stackTrace, LogType type)
        {
            if (_h8MemoryFatalDumpWritten)
                return;

            if (type != LogType.Exception &&
                type != LogType.Assert &&
                (type != LogType.Error || !IsFatalMemoryDumpCandidate(condition)))
            {
                return;
            }

            _h8MemoryFatalDumpWritten = true;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(logDirectory);
            string path = Path.Combine(logDirectory, "Dump_CORE_DATA_VAULT_WARDEN.txt");
            H8Memory.DumpAllocationTableText(path);
        }

        private static bool IsFatalMemoryDumpCandidate(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            return condition.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("crash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("nan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureGlobalDataVaultRegistered(
            long arenaCapacityLimitBytes,
            int bufferCapacity,
            in VaultMemoryLayoutConfig authoredConfig,
            bool hasAuthoredConfig)
        {
            if (_globalDataVault == null)
                _globalDataVault = GlobalDataVault.Create(bufferCapacity, arenaCapacityLimitBytes);

            if (!ReferenceEquals(GlobalRegistry.DataVault, _globalDataVault))
                GlobalRegistry.RegisterDataVault(_globalDataVault);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            VaultLegacyBinaryArchaeology.TryBootstrapMemoryLayout(
                _globalDataVault,
                projectRoot,
                GlobalRegistry.ScalabilityTierProfileByte,
                out _);
            if (hasAuthoredConfig)
                VaultLegacyBinaryArchaeology.WriteMemoryLayoutConfig(_globalDataVault, in authoredConfig);

            if (!string.IsNullOrEmpty(projectRoot))
            {
                string overrideCsvPath = Path.Combine(projectRoot, "memory_overrides.csv");
                VaultLegacyBinaryArchaeology.TryApplyMemoryOverridesCsv(_globalDataVault, overrideCsvPath);
            }

            PreallocateDataVaultPrimaryBuffers(_globalDataVault);
        }

        private static void PreallocateDataVaultPrimaryBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

            vault.GetBuffer<double>(
                BufferID.H8Time,
                (int)H8TimeSlot.Count,
                SystemID.SystemDispatcher,
                NativeArrayOptions.ClearMemory);
            vault.GetBuffer<double3>(
                BufferID.RigidbodyAUPs,
                512,
                SystemID.GlobalPhysicsStateManager,
                NativeArrayOptions.ClearMemory);
        }

        private static void InitializeBootstrapEventBuses()
        {
            GlobalTelemetryBus.Initialize();
            GlobalSignals.InitializeAllQueues();
            EnsureSimulationBucketerRegistered();
            EnsureJobAdmissionServiceRegistered();
        }

        private static void InitializeBootstrapMmfStorage()
        {
            PreInitAssetIdMap.Initialize();
            EnsureMacroDatabaseRegistered();
        }

        private static void EnsureMacroDatabaseRegistered()
        {
            if (_macroDatabaseService == null)
                _macroDatabaseService = new H8MacroDatabaseService();

            if (ReferenceEquals(GlobalRegistry.MacroDatabase, _macroDatabaseService))
                return;

            string databaseDirectory = Path.Combine(Application.persistentDataPath, "H8_MacroDB");
            string databasePath = Path.Combine(databaseDirectory, "macro_world.h8db");
            MacroDatabaseConfig config = MacroDatabaseConfig.Default;
            MacroDatabaseSignalBridge signalBridge = new MacroDatabaseSignalBridge();
            if (!_macroDatabaseService.Initialize(databasePath, in config, _globalDataVault, signalBridge))
            {
                _macroDatabaseService.Shutdown();
                _macroDatabaseService = null;
                return;
            }

            GlobalRegistry.RegisterMacroDatabase(_macroDatabaseService);
        }

        private static bool InitializeBootstrapDataMonolith(uint appVersionHash)
        {
#if UNITY_EDITOR
            bool failIfMissing = false;
#else
            bool failIfMissing = true;
#endif
            bool loaded = global::Hecton8.Data.H8StaticDataArena.TryInitializeFromStreamingAssets(
                0u,
                appVersionHash,
                failIfMissing,
                out global::Hecton8.Data.H8DataBlobLoadStatus dataStatus);

#if UNITY_EDITOR
            return loaded || dataStatus == global::Hecton8.Data.H8DataBlobLoadStatus.Missing;
#else
            if (!loaded)
                throw new global::Hecton8.Core.FatalArchitectureException("Data Monolith boot failure: " + dataStatus);

            return true;
#endif
        }

        private async Awaitable<bool> InitializeCoreServicesPhaseAsync(CancellationToken ct)
        {
            try
            {
                if (!await JoinBackgroundDomainHandshakeAsync(ct))
                    return false;

                bool initialized = await InitializeCoreLayerAsync(ct);
                if (initialized && !await WarmObjectPoolPresetsAsync(ct))
                    return false;
                if (initialized && !await InitializePresentationBootstrapAsync(ct))
                    return false;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return initialized;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Awaitable<bool> WarmObjectPoolPresetsAsync(CancellationToken ct)
        {
            ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;
            if (objectPoolManager == null)
                objectPoolManager = ObjectPoolManager.ActiveRuntimeInstance;

            if (objectPoolManager == null || objectPoolManager.AreWarmupPresetsCompleted)
                return true;

            return await objectPoolManager.WarmupPresetsAsync(
                ObjectPoolWarmupFrameBudgetMilliseconds,
                ct);
        }

        private async Awaitable<bool> InitializeEnvironmentPhaseAsync(CancellationToken ct)
        {
            try
            {
                bool initialized = await InitializeEnvironmentLayerAsync(ct);
                if (initialized && !await WarmEnvironmentObjectPoolsAsync(ct))
                    return false;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return initialized;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Awaitable<bool> WarmEnvironmentObjectPoolsAsync(CancellationToken ct)
        {
            ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;
            RandomEventSystem randomEvents = GlobalRegistry.RandomEvents;
            if (objectPoolManager == null || randomEvents == null)
                return true;

            return await randomEvents.WarmMeteorSplashPoolAsync(
                objectPoolManager,
                ObjectPoolWarmupFrameBudgetMilliseconds,
                ct);
        }

        private async Awaitable<bool> InitializePlayerPhaseAsync(CancellationToken ct)
        {
            try
            {
                bool initialized = await InitializePlayerLayerAsync(ct);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return initialized;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeUIPhaseAsync(CancellationToken ct)
        {
            try
            {
                if (!await InitializeUILayerAsync(ct))
                    return false;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeSceneActivatePhaseAsync(CancellationToken ct)
        {
            try
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

                if (!_sceneActivationRequested && BootstrapState.IsGameReady)
                    return true;

                _sceneActivationRequested = true;
                BootstrapState.PublishBootstrapPresence(true);
                return await ExecuteSceneActivationAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> RunSceneActivationAsync(CancellationToken ownerToken)
        {
            try
            {
                bool activated = await ExecuteSceneActivationAsync(ownerToken);
                if (activated)
                    GlobalRegistry.LockReady();

                return activated;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(nameof(BootstrapPhase.SceneActivate), exception);
                return false;
            }
            finally
            {
                _sceneActivationRequested = false;
                _sceneActivationRunInProgress = false;
            }
        }

        private async Awaitable<bool> ExecuteSceneActivationAsync(CancellationToken ct)
        {
            try
            {
                return await ExecuteSceneReadinessGatesAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Awaitable<bool> LoadMainMenuAsync(CancellationToken ct)
        {
            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
                if (loadOperation == null)
                    return false;

                loadOperation.allowSceneActivation = false;
                int waitFrames = 0;
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                while (loadOperation.progress < 0.9f)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapSceneLoadWatchdogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapSceneLoadWatchdog("main-menu load", loadOperation.progress, waitFrames, elapsedSeconds);
                        return false;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                if (!await WaitForBootstrapActivationGatesAsync(ct))
                    return false;

                if (!TryValidateSceneRootBudget(MainMenuSceneName, "bootstrap-main-menu-preactivation"))
                    return false;

                SceneRuntimeService.ReleaseSceneActivation(loadOperation);
                waitFrames = 0;
                waitStartTimestamp = Stopwatch.GetTimestamp();
                while (!loadOperation.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapSceneLoadWatchdogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapSceneLoadWatchdog("main-menu activation", loadOperation.progress, waitFrames, elapsedSeconds);
                        return false;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                BootstrapStatus.MarkMainMenuReached();
                return true;
            }
            catch (OperationCanceledException)
            {
                if (loadOperation != null && !loadOperation.isDone)
                    SceneRuntimeService.ReleaseSceneActivation(loadOperation);

                return false;
            }
        }

        private static void LogBootstrapSceneLoadWatchdog(string stageName, float progress, int waitFrames, double elapsedSeconds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Scene load watchdog tripped during {stageName}. progress={progress:0.000} frames={waitFrames} elapsed={elapsedSeconds:0.000}s target={MainMenuSceneName}.");
#endif
        }

        private async Awaitable<bool> InitializeCoreLayerAsync(CancellationToken ct)
        {
            EnsureCrashTelemetryBufferRegistered();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimeWatchdogRegistered();
            EnsureGCMonitorRegistered();
            EnsureRuntimePerformanceProfilerRegistered();
#endif
            ThreadSafeCommandQueue.Initialize();
            EnsurePrefabRegistry();
            EnsurePersistentWorldRegistry();
            return await InitializeBootstrapLayerNodesAsync(BootstrapPhase.CoreServices, ct);
        }

        private async Awaitable<bool> InitializeEnvironmentLayerAsync(CancellationToken ct)
        {
            return await InitializeBootstrapLayerNodesAsync(BootstrapPhase.Environment, ct);
        }

        private async Awaitable<bool> InitializePlayerLayerAsync(CancellationToken ct)
        {
            if (!InputManager.TryValidateRuntimeConfiguration(out string inputConfigurationError))
            {
                BootstrapBiosErrorOverlay.Show(inputConfigurationError);
                return false;
            }

            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager == null)
                inputManager = ResolveBootstrapInputManager(gameObject.scene);
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

            if (!ReferenceEquals(GlobalRegistry.NativeInputManager, inputManager))
                GlobalRegistry.RegisterNativeInputManagerRuntime(inputManager);

            PersistRuntimeService(inputManager);
            UserOptionsPersistence userOptionsPersistence = GlobalRegistry.UserOptions;
            if (userOptionsPersistence == null)
            {
                GameObject userOptionsRoot = new GameObject("[UserOptionsPersistence]"); // COLD ALLOC: GameObject[1] - bootstrap-owned user options persistence root - owner: GameBootstrapper
                userOptionsPersistence = userOptionsRoot.AddComponent<UserOptionsPersistence>();
            }

            PersistRuntimeService(userOptionsPersistence);
            if (userOptionsPersistence != null && !ReferenceEquals(GlobalRegistry.UserOptions, userOptionsPersistence))
                GlobalRegistry.RegisterUserOptionsRuntime(userOptionsPersistence);

            RebindingManager rebindingManager = RebindingManager.ActiveRuntimeInstance;
            if (rebindingManager == null)
            {
                GameObject rebindingRoot = new GameObject("[RebindingManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned input binding service - owner: GameBootstrapper
                rebindingManager = rebindingRoot.AddComponent<RebindingManager>();
            }

            rebindingManager.BindNativeInputManager(inputManager);
            PersistRuntimeService(rebindingManager);

            ContextualPhysicalIkRuntime contextualIkRuntime = ContextualPhysicalIkRuntime.EnsureRuntimeInstance();
            PersistRuntimeService(contextualIkRuntime);
            VRSomaticRuntimeBootstrap vrSomaticRuntime = VRSomaticRuntimeBootstrap.EnsureRegisteredByBootstrap();
            PersistRuntimeService(vrSomaticRuntime);
            if (!await InitializeBootstrapLayerNodesAsync(BootstrapPhase.Player, ct))
                return false;

            PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
            playerContextService.RefreshRuntimeContext();
            return true;
        }

        private static InputManager ResolveBootstrapInputManager(Scene scene)
        {
            if (_bootstrapInputManager != null)
                return _bootstrapInputManager;

            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                if (current.TryGetComponent(out InputManager inputManager) &&
                    inputManager != null)
                {
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return inputManager;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            return null;
        }

        private async Awaitable<bool> InitializeUILayerAsync(CancellationToken ct)
        {
            EnsureSettingsRuntimeRegistered();
            UIStateStore.EnsureInitialized();
#if UNITY_ADDRESSABLES_EXIST
            if (!await LoadAddressableDependencyChainAsync(ct))
                return false;

            if (!await LoadAddressableUIPrefabsAsync(ct))
                return false;
#endif
            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            return true;
        }

        private static SettingsManager EnsureSettingsRuntimeRegistered()
        {
            SettingsManager settingsManager = GlobalRegistry.Settings;
            if (settingsManager == null)
            {
                GameObject settingsRoot = new GameObject("[SettingsManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned settings runtime owner - owner: GameBootstrapper
                settingsManager = settingsRoot.AddComponent<SettingsManager>();
            }

            if (settingsManager == null)
                return null;

            PersistRuntimeService(settingsManager);
            if (!ReferenceEquals(GlobalRegistry.Settings, settingsManager))
                GlobalRegistry.RegisterSettingsRuntime(settingsManager);

            settingsManager.RefreshPersistenceFromRegistry();
            return settingsManager;
        }

        private async Awaitable<bool> LoadAddressableUIPrefabsAsync(CancellationToken ct)
        {
#if UNITY_ADDRESSABLES_EXIST
            int prefabCount = uiAddressablePrefabs != null ? uiAddressablePrefabs.Length : 0;
            if (prefabCount <= 0)
                return true;

            if (_uiPrefabInstanceHandles == null || _uiPrefabInstanceHandles.Length != prefabCount)
            {
                ReleaseAddressableUIPrefabs();
                _uiPrefabInstanceHandles = new AsyncOperationHandle<GameObject>[prefabCount]; // COLD ALLOC: AsyncOperationHandle<GameObject>[uiAddressablePrefabs.Length] - UI bootstrap readiness handles - owner: GameBootstrapper
            }

            for (int i = 0; i < prefabCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                AssetReferenceGameObject prefabReference = uiAddressablePrefabs[i];
                if (prefabReference == null || !prefabReference.RuntimeKeyIsValid())
                    continue;

                AsyncOperationHandle<GameObject> existingHandle = _uiPrefabInstanceHandles[i];
                if (existingHandle.IsValid())
                    continue;

                _uiPrefabInstanceHandles[i] = prefabReference.InstantiateAsync(transform);
            }

            for (int i = 0; i < prefabCount; i++)
            {
                AsyncOperationHandle<GameObject> handle = _uiPrefabInstanceHandles[i];
                if (!handle.IsValid())
                    continue;

                while (!handle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                    handle = _uiPrefabInstanceHandles[i];
                }

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[GameBootstrapper] UI addressable prefab failed during bootstrap UI gate.");
#endif
                    return false;
                }
            }

            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            return true;
#else
            ct.ThrowIfCancellationRequested();
            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            return true;
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private static async Awaitable<bool> PreWarmTierAddressableTextureGroupAsync(CancellationToken ct)
        {
            string label = ResolveTierAddressableTextureLabel();
#if UNITY_EDITOR
            if (!HasEditorAddressablesRuntimeSettingsFile())
            {
                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent("bootstrap.addressables.tier_prewarm.skipped_missing_runtime_settings", label);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
#endif
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(label, false);
            while (!handle.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }

            bool succeeded = handle.Status == AsyncOperationStatus.Succeeded;
            if (succeeded)
                PublishAddressableDependencyGroupLoaded(-1, label, handle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.LogError("[GameBootstrapper] Tier Addressables prewarm failed: " + label);
#endif
            Addressables.Release(handle);
            return succeeded;
        }

#if UNITY_EDITOR
        private static bool HasEditorAddressablesRuntimeSettingsFile()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string settingsPath = Path.Combine(projectRoot, "Library", "com.unity.addressables", "aa", "Windows", "settings.json");
            return File.Exists(settingsPath);
        }

#endif

        private static string ResolveTierAddressableTextureLabel()
        {
            return GlobalRegistry.MathPrecision == MathPrecisionLevel.High
                ? TierHighAddressableLabel
                : TierLowAddressableLabel;
        }

        private async Awaitable<bool> LoadAddressableDependencyChainAsync(CancellationToken ct)
        {
            int groupCount = addressableDependencyGroups != null ? addressableDependencyGroups.Length : 0;
            for (int i = 0; i < groupCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                AssetLabelReference group = addressableDependencyGroups[i];
                if (group == null || string.IsNullOrEmpty(group.labelString))
                    continue;

                AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(group.labelString, false);
                while (!handle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Addressables.Release(handle);
                    return false;
                }

                PublishAddressableDependencyGroupLoaded(i, group.labelString, handle);
                Addressables.Release(handle);
            }

            return true;
        }

        private static void PublishAddressableDependencyGroupLoaded(
            int dependencyIndex,
            string label,
            AsyncOperationHandle handle)
        {
            uint groupHash = ComputeAddressableGroupHash(label);
            AssetLifecycleGovernor lifecycleGovernor = GlobalRegistry.AssetLifecycle;
            if (lifecycleGovernor != null)
                lifecycleGovernor.MarkAddressableDependencyGroupLoaded(groupHash, dependencyIndex, handle);

            AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
            if (dispatcher != null)
                dispatcher.MarkAddressableDependencyGroupReady(groupHash, dependencyIndex, handle);
        }

        private static uint ComputeAddressableGroupHash(string label)
        {
            if (string.IsNullOrEmpty(label))
                return 0u;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= 16777619u;
                }

                return hash != 0u ? hash : 1u;
            }
        }
#endif

        private void ReleaseAddressableUIPrefabs()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_uiPrefabInstanceHandles == null)
                return;

            for (int i = 0; i < _uiPrefabInstanceHandles.Length; i++)
            {
                AsyncOperationHandle<GameObject> handle = _uiPrefabInstanceHandles[i];
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle);

                _uiPrefabInstanceHandles[i] = default;
            }
#endif
        }

        private async Awaitable<bool> WarmConfiguredShaderVariantCollectionsAsync(CancellationToken ct)
        {
            try
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

                    _ = collection.isWarmedUp;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static void WarmMathLodShaderKeywords()
        {
            if (GlobalRegistry.MathPrecision == MathPrecisionLevel.High)
            {
                Shader.DisableKeyword(MathLodLowKeyword);
                Shader.EnableKeyword(MathLodHighKeyword);
                DistanceMath.PushShaderMathLod(MathLodMode.High);
                return;
            }

            Shader.EnableKeyword(MathLodLowKeyword);
            Shader.DisableKeyword(MathLodHighKeyword);
            DistanceMath.PushShaderMathLod(
                DistanceMath.ResolveMathLodMode(GlobalRegistry.ScalabilityTier));
        }

        private static bool AreBootstrapActivationGatesReady()
        {
            if (!_preWarmAssetsReady)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            return registry != null && registry.AreResidentWorldPrefabPoolsReady();
        }

        private static bool HasWatchdogElapsed(long startTimestamp, double timeoutSeconds, out double elapsedSeconds)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
            return elapsedSeconds >= timeoutSeconds;
        }

        private static async Awaitable<bool> WaitForBootstrapActivationGatesAsync(CancellationToken ct)
        {
            try
            {
                int waitFrames = 0;
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                while (!AreBootstrapActivationGatesReady())
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapJobWaitWatchdogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapSceneLoadWatchdog("asset activation gates", 0.9f, waitFrames, elapsedSeconds);
                        return false;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeBootstrapLayerNodesAsync(BootstrapPhase phase, CancellationToken ct)
        {
            if (_bootstrapExecutionOrderCount != (int)BootstrapDependencyNode.Count &&
                !TryBuildBootstrapDependencyExecutionOrder(_bootstrapExecutionOrder, out _bootstrapExecutionOrderCount))
            {
                LogBootstrapDependencyGraphFailure(phase);
                return false;
            }

            for (int orderIndex = 0; orderIndex < _bootstrapExecutionOrderCount; orderIndex++)
            {
                BootstrapDependencyNode node = _bootstrapExecutionOrder[orderIndex];
                if (ResolveBootstrapNodePhase(node) != phase)
                    continue;

                WriteBootStateRecord(BootStateMarker.ServiceStarted, phase, ResolveRegistrySlotForBootstrapNode(node));
                long serviceStartTimestamp = Stopwatch.GetTimestamp();
                try
                {
                    if (!TryInitializeBootstrapDependencyNodeWithFallback(node))
                    {
                        LogBootstrapDependencyFailure(phase, node);
                        return false;
                    }

                    if (!await WaitForBootstrapDependencyHeartbeatAsync(node, ct))
                    {
                        LogBootstrapDependencyFailure(phase, node);
                        return false;
                    }

                }
                finally
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    double elapsedMilliseconds =
                        (Stopwatch.GetTimestamp() - serviceStartTimestamp) * 1000.0 / Stopwatch.Frequency;
                    BootstrapHealthMonitor.RecordServiceDuration((int)node, elapsedMilliseconds);
#endif
                }
            }

            return true;
        }

        private static async Awaitable<bool> WaitForBootstrapDependencyHeartbeatAsync(
            BootstrapDependencyNode node,
            CancellationToken ct)
        {
            int waitFrames = 0;
            long waitStartTimestamp = Stopwatch.GetTimestamp();
            while (!IsBootstrapDependencyHeartbeatReady(node))
            {
                ct.ThrowIfCancellationRequested();
                if (HasWatchdogElapsed(waitStartTimestamp, OptionalServiceTimeoutMilliseconds * 0.001d, out double elapsedSeconds))
                {
                    LogBootstrapHeartbeatFailure(node, waitFrames, elapsedSeconds);
                    TriggerServiceEmergencyReset(node);
                    return false;
                }

                waitFrames++;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }

            return true;
        }

        private static bool IsBootstrapDependencyHeartbeatReady(BootstrapDependencyNode node)
        {
            object service = ResolveBootstrapDependencyService(node);
            if (service is IServiceHeartbeat heartbeat)
                return heartbeat.IsServiceReady && heartbeat.HeartbeatState != ServiceHeartbeatState.Failed;

            return IsBootstrapDependencyNodeReady(node, service);
        }

        private static bool IsBootstrapDependencyNodeReady(BootstrapDependencyNode node)
        {
            return IsBootstrapDependencyNodeReady(node, ResolveBootstrapDependencyService(node));
        }

        private static bool IsBootstrapDependencyNodeReady(BootstrapDependencyNode node, object service)
        {
            switch (node)
            {
                case BootstrapDependencyNode.RenderDispatcher:
                    return _headlessBootMode || service != null;
                case BootstrapDependencyNode.HectonFloatingOrigin:
                    return service != null && !HectonFloatingOrigin.IsShiftInProgress;
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer:
                    return _headlessBootMode || service != null;
                case BootstrapDependencyNode.DebrisManager:
                    return true;
                case BootstrapDependencyNode.FaunaSimulation:
                    return service is IFaunaSim faunaSimulation && faunaSimulation.IsReady;
                case BootstrapDependencyNode.SpatialAudioManager:
                    return _headlessBootMode || service != null;
                case BootstrapDependencyNode.ConstructionManager:
                    return service != null || GlobalRegistry.Logistics == null;
                default:
                    return service != null;
            }
        }

        private static object ResolveBootstrapDependencyService(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher: return GlobalRegistry.Dispatcher;
                case BootstrapDependencyNode.GameTickManager: return GlobalRegistry.TickManager;
                case BootstrapDependencyNode.SaveManager: return GlobalRegistry.Save;
                case BootstrapDependencyNode.ObjectPoolManager: return GlobalRegistry.ObjectPool;
                case BootstrapDependencyNode.RenderDispatcher: return GlobalRegistry.RenderDispatcher;
                case BootstrapDependencyNode.SceneRuntimeService: return GlobalRegistry.Scene;
                case BootstrapDependencyNode.EquipmentInteractionHandler: return GlobalRegistry.InteractionSignals;
                case BootstrapDependencyNode.HectonFloatingOrigin: return GlobalRegistry.FloatingOrigin;
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer: return GlobalRegistry.ConnectionSplineBatchRenderer;
                case BootstrapDependencyNode.GlobalPhysicsStateManager: return GlobalRegistry.PhysicsStateManager;
                case BootstrapDependencyNode.PhysicsApplySystem: return GlobalRegistry.Physics;
                case BootstrapDependencyNode.DebrisManager: return GlobalRegistry.DebrisCompute;
                case BootstrapDependencyNode.EnvironmentRuntimeContextService: return GlobalRegistry.Environment;
                case BootstrapDependencyNode.OceanKinematicsRuntimeService: return GlobalRegistry.OceanKinematics;
                case BootstrapDependencyNode.EcosystemDirector: return GlobalRegistry.EcosystemDirector;
                case BootstrapDependencyNode.FaunaSimulation: return GlobalRegistry.FaunaSimulation;
                case BootstrapDependencyNode.SpatialAudioManager: return GlobalRegistry.Audio;
                case BootstrapDependencyNode.NativeInputManager: return GlobalRegistry.NativeInputManager;
                case BootstrapDependencyNode.InputDispatcher: return GlobalRegistry.RegisteredInput;
                case BootstrapDependencyNode.PlayerRuntimeContextService: return GlobalRegistry.Player;
                case BootstrapDependencyNode.PlayerInventoryManager: return GlobalRegistry.PlayerInventory;
                case BootstrapDependencyNode.PlayerSensoryManager: return GlobalRegistry.PlayerSensory;
                case BootstrapDependencyNode.PowerGridManager: return GlobalRegistry.PowerGrid;
                case BootstrapDependencyNode.ConstructionManager: return GlobalRegistry.ConstructionRuntime;
                case BootstrapDependencyNode.BeaconNetworkSystem: return GlobalRegistry.BeaconNetwork;
                case BootstrapDependencyNode.ModWorldPersistenceManager: return GlobalRegistry.ModWorldPersistence;
                default: return null;
            }
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
                case BootstrapDependencyNode.ModWorldPersistenceManager:
                    return BootstrapPhase.CoreServices;

                case BootstrapDependencyNode.HectonFloatingOrigin:
                case BootstrapDependencyNode.GlobalPhysicsStateManager:
                case BootstrapDependencyNode.PhysicsApplySystem:
                case BootstrapDependencyNode.DebrisManager:
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer:
                case BootstrapDependencyNode.EnvironmentRuntimeContextService:
                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                case BootstrapDependencyNode.EcosystemDirector:
                case BootstrapDependencyNode.FaunaSimulation:
                case BootstrapDependencyNode.SpatialAudioManager:
                case BootstrapDependencyNode.PowerGridManager:
                case BootstrapDependencyNode.ConstructionManager:
                    return BootstrapPhase.Environment;

                case BootstrapDependencyNode.NativeInputManager:
                case BootstrapDependencyNode.InputDispatcher:
                case BootstrapDependencyNode.PlayerRuntimeContextService:
                case BootstrapDependencyNode.PlayerInventoryManager:
                case BootstrapDependencyNode.PlayerSensoryManager:
                case BootstrapDependencyNode.BeaconNetworkSystem:
                    return BootstrapPhase.Player;

                default:
                    return BootstrapPhase.Fatal;
            }
        }

        private static bool TryInitializeBootstrapDependencyNodeWithFallback(BootstrapDependencyNode node)
        {
            try
            {
                return InitializeBootstrapDependencyNode(node);
            }
            catch (Exception exception)
            {
                if (TryRegisterStableFallbackForBootstrapNode(node, exception))
                    return true;

                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent(
                    "bootstrap.service.init.exception",
                    ResolveBootstrapDependencyNodeName(node));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GameBootstrapper] Bootstrap dependency exception. node=" +
                    ResolveBootstrapDependencyNodeName(node) +
                    " error=" +
                    exception.Message);
#endif
                return false;
            }
        }

        private static bool TryRegisterStableFallbackForBootstrapNode(
            BootstrapDependencyNode node,
            Exception exception)
        {
            if (node == BootstrapDependencyNode.SpatialAudioManager)
                return TryRegisterNoOpAudioFallback(exception != null ? exception.Message : "SpatialAudioManager init exception");

            GlobalRegistryServiceSlot slot = ResolveRegistrySlotForBootstrapNode(node);
            if (GlobalRegistry.TryReplaceBootstrapServiceWithStableProxy(slot))
            {
                LogOptionalBootstrapWarning("Injected stable bootstrap proxy for " + ResolveBootstrapDependencyNodeName(node));
                return true;
            }

            return false;
        }

        private static bool InitializeBootstrapDependencyNode(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher:
                    return EnsureSystemDispatcherRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.GameTickManager:
                    return EnsureGameTickManagerRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.SaveManager:
                    return EnsureSaveServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.ObjectPoolManager:
                    return EnsureObjectPoolServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.RenderDispatcher:
                    if (_headlessBootMode)
                        return true;

                    return EnsureRenderDispatcherRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.SceneRuntimeService:
                {
                    SceneRuntimeService sceneRuntimeService = SceneRuntimeService.EnsureRuntimeInstance();
                    if (sceneRuntimeService == null)
                        return false;

                    PersistRuntimeService(sceneRuntimeService);
                    sceneRuntimeService.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.EquipmentInteractionHandler:
                    return EnsureEquipmentInteractionServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.HectonFloatingOrigin:
                    return EnsureFloatingOriginRegistered() != null && GlobalRegistry.FloatingOrigin != null;

                case BootstrapDependencyNode.ConnectionSplineBatchRenderer:
                    if (_headlessBootMode)
                        return true;

                    return EnsureConnectionSplineBatchRendererRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.GlobalPhysicsStateManager:
                    return EnsureGlobalPhysicsStateManagerRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.PhysicsApplySystem:
                {
                    PhysicsApplySystem physicsApplySystem = EnsurePhysicsApplySystemRegistered();
                    if (physicsApplySystem == null)
                        return false;

                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.DebrisManager:
                {
                    return true;
                }

                case BootstrapDependencyNode.EnvironmentRuntimeContextService:
                {
                    EnvironmentRuntimeContextService environmentContextService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
                    if (environmentContextService == null)
                        return false;

                    PersistRuntimeService(environmentContextService);
                    environmentContextService.InitializeService();
                    HectonSeismicTideDirector seismicTideDirector = HectonSeismicTideDirector.EnsureRuntimeInstance();
                    if (seismicTideDirector != null)
                    {
                        PersistRuntimeService(seismicTideDirector);
                        seismicTideDirector.InitializeService();
                    }

                    return GlobalRegistry.Environment != null && GlobalRegistry.SeismicDirector != null;
                }

                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                {
                    OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
                    if (oceanKinematicsRuntimeService == null)
                        return false;

                    PersistRuntimeService(oceanKinematicsRuntimeService);
                    oceanKinematicsRuntimeService.InitializeService();
                    if (!_headlessBootMode)
                        TryEnsureAnalyticalCausticsRegistered();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.EcosystemDirector:
                    return EnsureEcosystemDirectorRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.FaunaSimulation:
                    return EnsureFaunaSimulationRegistered();

                case BootstrapDependencyNode.SpatialAudioManager:
                    if (_headlessBootMode)
                        return true;

                    return InitializeSpatialAudioBootstrapNode();

                case BootstrapDependencyNode.ConstructionManager:
                {
                    ConstructionManager constructionManager = EnsureConstructionServiceRegistered();
                    return constructionManager == null || GlobalRegistry.ConstructionRuntime != null;
                }

                case BootstrapDependencyNode.NativeInputManager:
                    return EnsureNativeInputManagerRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.InputDispatcher:
                    return EnsureInputDispatcherRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.PlayerRuntimeContextService:
                {
                    PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
                    if (playerContextService == null)
                        return false;

                    PersistRuntimeService(playerContextService);
                    playerContextService.InitializeServiceDeferredSync();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.PlayerInventoryManager:
                {
                    PlayerInventoryManager playerInventoryManager = PlayerInventoryManager.EnsureRuntimeInstance();
                    if (playerInventoryManager == null)
                        return false;

                    PersistRuntimeService(playerInventoryManager);
                    playerInventoryManager.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.PlayerSensoryManager:
                {
                    PlayerSensoryManager playerSensoryManager = PlayerSensoryManager.EnsureRuntimeInstance();
                    if (playerSensoryManager == null)
                        return false;

                    PersistRuntimeService(playerSensoryManager);
                    playerSensoryManager.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.BeaconNetworkSystem:
                    return EnsureBeaconNetworkServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.ModWorldPersistenceManager:
                    return EnsureModWorldPersistenceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.PowerGridManager:
                    return EnsurePowerGridServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                default:
                    return false;
            }
        }

        private static SystemDispatcher EnsureSystemDispatcherRegistered()
        {
            EnsureSimulationBucketerRegistered();
            EnsureJobAdmissionServiceRegistered();

            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher == null)
                dispatcher = SystemDispatcher.ActiveRuntimeInstance;

            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[SystemDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned gameplay dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<SystemDispatcher>();
            }

            PersistRuntimeService(dispatcher);

            dispatcher.InitializeService();
            ActiveInstance?.TryRegisterBootstrapSlowTickable();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimeWatchdogRegistered();
            EnsureGCMonitorRegistered();
#endif
            return dispatcher;
        }

        private static ISimulationBucketer EnsureSimulationBucketerRegistered()
        {
            ISimulationBucketer registered = GlobalRegistry.SimulationBucketer;
            if (registered != null)
            {
                if (registered is ModuloSimulationBucketer moduloBucketer)
                    moduloBucketer.Initialize(SimulationBucketConstants.DefaultEntityCapacity, GlobalRegistry.DataVault);
                else if (!registered.IsInitialized)
                    registered.Initialize(SimulationBucketConstants.DefaultEntityCapacity);

                return registered;
            }

            if (_simulationBucketerService == null)
                _simulationBucketerService = new ModuloSimulationBucketer(); // COLD ALLOC: ModuloSimulationBucketer[1] - bootstrap-owned simulation cadence slicer - owner: GameBootstrapper

            _simulationBucketerService.Initialize(SimulationBucketConstants.DefaultEntityCapacity, GlobalRegistry.DataVault);
            GlobalRegistry.RegisterSimulationBucketerRuntime(_simulationBucketerService);
            return _simulationBucketerService;
        }

        private static IJobAdmissionService EnsureJobAdmissionServiceRegistered()
        {
            IJobAdmissionService registered = GlobalRegistry.JobAdmission;
            if (registered != null)
            {
                if (_jobAdmissionTelemetryBridge == null)
                    _jobAdmissionTelemetryBridge = new JobAdmissionTelemetryBridge(); // COLD ALLOC: JobAdmissionTelemetryBridge[1] - scheduler telemetry bridge - owner: GameBootstrapper

                if (!registered.IsInitialized)
                {
                    if (registered is BurstTokenBucketJobAdmissionService burstAdmissionService)
                        burstAdmissionService.Initialize(_jobAdmissionTelemetryBridge, GlobalRegistry.DataVault);
                    else
                        registered.Initialize(_jobAdmissionTelemetryBridge);
                }

                JobAdmissionSchedulerBridge.SetService(registered);
                return registered;
            }

            if (_jobAdmissionTelemetryBridge == null)
                _jobAdmissionTelemetryBridge = new JobAdmissionTelemetryBridge(); // COLD ALLOC: JobAdmissionTelemetryBridge[1] - scheduler telemetry bridge - owner: GameBootstrapper

            if (_jobAdmissionService == null)
                _jobAdmissionService = new BurstTokenBucketJobAdmissionService(); // COLD ALLOC: BurstTokenBucketJobAdmissionService[1] - bootstrap-owned job admission gate - owner: GameBootstrapper

            _jobAdmissionService.Initialize(_jobAdmissionTelemetryBridge, GlobalRegistry.DataVault);
            GlobalRegistry.RegisterJobAdmissionRuntime(_jobAdmissionService);
            JobAdmissionSchedulerBridge.SetService(_jobAdmissionService);
            return _jobAdmissionService;
        }

        private static bool TryEnsureAnalyticalCausticsRegistered()
        {
            if (GlobalRegistry.Caustics != null)
                return true;

            Type serviceType = Type.GetType("Hecton8.Graphics.Caustics.AnalyticalCausticsService, Hecton8.Graphics.Caustics", false);
            if (serviceType == null)
                return false;

            MethodInfo ensureMethod = serviceType.GetMethod("EnsureRuntimeInstance", BindingFlags.Public | BindingFlags.Static);
            Component serviceComponent = ensureMethod != null ? ensureMethod.Invoke(null, null) as Component : null;
            if (serviceComponent == null)
                return false;

            PersistRuntimeService(serviceComponent);
            GameBootstrapper bootstrapper = ActiveInstance;
            ComputeShader computeShader = bootstrapper != null
                ? bootstrapper.analyticalCausticsCompute
                : null;
            if (computeShader != null)
            {
                MethodInfo assignComputeMethod = serviceType.GetMethod("AssignComputeShader", BindingFlags.Public | BindingFlags.Instance);
                if (assignComputeMethod != null)
                {
                    try
                    {
                        _bootstrapReflectionSingleArgumentScratch[0] = computeShader;
                        assignComputeMethod.Invoke(serviceComponent, _bootstrapReflectionSingleArgumentScratch);
                    }
                    finally
                    {
                        _bootstrapReflectionSingleArgumentScratch[0] = null;
                    }
                }
            }

            MethodInfo initializeMethod = serviceType.GetMethod("InitializeService", BindingFlags.Public | BindingFlags.Instance);
            initializeMethod?.Invoke(serviceComponent, null);
            return GlobalRegistry.Caustics != null;
        }

        internal static void PersistRuntimeService(Component component)
        {
            if (!Application.isPlaying || component == null)
                return;

            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper == null)
                return;

            Transform bootstrapTransform = bootstrapper.transform;
            Transform componentTransform = component.transform;
            if (componentTransform == bootstrapTransform)
                return;

            if (componentTransform.parent != bootstrapTransform)
                componentTransform.SetParent(bootstrapTransform, true);

            EnforceProjectPersistentRoot();
        }

        private static GameTickManager EnsureGameTickManagerRegistered()
        {
            GameTickManager tickManager = GlobalRegistry.TickManager;
            if (tickManager == null)
                tickManager = GameTickManager.ActiveRuntimeInstance;

            if (tickManager == null)
            {
                GameObject runtimeRoot = new GameObject("[GameTickManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned tick manager root - owner: GameBootstrapper
                tickManager = runtimeRoot.AddComponent<GameTickManager>();
            }

            PersistRuntimeService(tickManager);

            tickManager.InitializeService();
            return tickManager;
        }

        private static SaveManager EnsureSaveServiceRegistered()
        {
            SaveManager saveManager = GlobalRegistry.SaveRuntime;

            if (saveManager == null)
            {
                GameObject runtimeRoot = new GameObject("[SaveManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned save manager root - owner: GameBootstrapper
                saveManager = runtimeRoot.AddComponent<SaveManager>();
            }

            PersistRuntimeService(saveManager);

            saveManager.InitializeService();
            return saveManager;
        }

        private static ObjectPoolManager EnsureObjectPoolServiceRegistered()
        {
            ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;
            if (objectPoolManager == null)
                objectPoolManager = ObjectPoolManager.ActiveRuntimeInstance;

            if (objectPoolManager == null)
            {
                GameObject runtimeRoot = new GameObject("[ObjectPoolManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned object pool root - owner: GameBootstrapper
                objectPoolManager = runtimeRoot.AddComponent<ObjectPoolManager>();
            }

            PersistRuntimeService(objectPoolManager);

            objectPoolManager.InitializeService();
            return objectPoolManager;
        }

        private static ModWorldPersistenceManager EnsureModWorldPersistenceRegistered()
        {
            ModWorldPersistenceManager manager = GlobalRegistry.ModWorldPersistence;
            if (manager == null)
            {
                GameObject runtimeRoot = new GameObject("[ModWorldPersistenceManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned mod world persistence root - owner: GameBootstrapper
                manager = runtimeRoot.AddComponent<ModWorldPersistenceManager>();
            }

            PersistRuntimeService(manager);
            manager.InitializeService();
            return manager;
        }

        private static RenderDispatcher EnsureRenderDispatcherRegistered()
        {
            RenderDispatcher dispatcher = GlobalRegistry.RenderDispatcher;
            if (dispatcher == null)
                dispatcher = RenderDispatcher.ActiveRuntimeInstance;

            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[RenderDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned SRP render dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<RenderDispatcher>();
            }

            PersistRuntimeService(dispatcher);
            dispatcher.InitializeService();
            return dispatcher;
        }

        private static EquipmentInteractionHandler EnsureEquipmentInteractionServiceRegistered()
        {
            if (GlobalRegistry.InteractionSignals is EquipmentInteractionHandler registeredHandler)
                return registeredHandler;

            EquipmentInteractionHandler interactionHandler = EquipmentInteractionHandler.ActiveRuntimeInstance;
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
            {
                PersistRuntimeService(telemetry);
            }

            BootstrapStatus.RegisterSafeHaltTelemetryReporter(CrashTelemetryBuffer.ReportBootstrapSafeHalt);
            return telemetry;
        }

        private static Hecton8.Core.RuntimeWatchdog EnsureRuntimeWatchdogRegistered()
        {
            Hecton8.Core.RuntimeWatchdog watchdog = Hecton8.Core.RuntimeWatchdog.EnsureRuntimeInstance();
            if (watchdog == null)
            {
                GameObject runtimeRoot = new GameObject(RuntimeWatchdogRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned runtime liveness watchdog root - owner: GameBootstrapper
                watchdog = runtimeRoot.AddComponent<Hecton8.Core.RuntimeWatchdog>();
            }

            PersistRuntimeService(watchdog);
            watchdog.InitializeService();
            return watchdog;
        }

        private static Hecton8.Core.GCMonitor EnsureGCMonitorRegistered()
        {
            Hecton8.Core.GCMonitor monitor = Hecton8.Core.GCMonitor.EnsureRuntimeInstance();
            if (monitor == null)
            {
                GameObject runtimeRoot = new GameObject(GCMonitorRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned GC sentinel root - owner: GameBootstrapper
                monitor = runtimeRoot.AddComponent<Hecton8.Core.GCMonitor>();
            }

            PersistRuntimeService(monitor);
            monitor.InitializeService();
            return monitor;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static RuntimePerformanceProfiler EnsureRuntimePerformanceProfilerRegistered()
        {
            RuntimePerformanceProfiler profiler = RuntimePerformanceProfiler.ActiveRuntime;
            bool activateAfterConfigure = false;
            if (profiler == null)
            {
                GameObject runtimeRoot = new GameObject(RuntimePerformanceProfilerRuntimeName); // COLD ALLOC: GameObject[1] - development performance profiler root - owner: GameBootstrapper
                runtimeRoot.SetActive(false);
                profiler = runtimeRoot.AddComponent<RuntimePerformanceProfiler>();
                activateAfterConfigure = true;
            }

            profiler.ConfigureForDevRun(
                autoStartOnEnable: true,
                enableBudgetViolationLogging: true,
                enableWindowLogging: false,
                autoStartNewGame: false,
                sampleWindow: 2f);

            PersistRuntimeService(profiler);
            if (activateAfterConfigure)
                profiler.gameObject.SetActive(true);

            return profiler;
        }
#endif

        private static PrefabRegistry EnsurePrefabRegistry()
        {
            PrefabRegistry registry = PrefabRegistry.ActiveRuntimeInstance;
            if (registry != null)
            {
                PersistRuntimeService(registry);
                return registry;
            }

            GameObject runtimeRoot = new GameObject(PrefabRegistryRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned prefab registry fallback - owner: GameBootstrapper
            PrefabRegistry createdRegistry = runtimeRoot.AddComponent<PrefabRegistry>();
            PersistRuntimeService(createdRegistry);
            return createdRegistry;
        }

        private static PersistentWorldRegistry EnsurePersistentWorldRegistry()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                return registry;

            GameObject runtimeRoot = new GameObject(PersistentWorldRegistryRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned persistent world registry fallback - owner: GameBootstrapper
            PersistentWorldRegistry createdRegistry = runtimeRoot.AddComponent<PersistentWorldRegistry>();
            PersistRuntimeService(createdRegistry);
            return createdRegistry;
        }

        private static HectonFloatingOrigin EnsureFloatingOriginRegistered()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin ?? HectonFloatingOrigin.EnsureRuntimeInstance();

            PersistRuntimeService(origin);

            origin.InitializeService();
            return origin;
        }

        private static ConnectionSplineBatchRenderer EnsureConnectionSplineBatchRendererRegistered()
        {
            ConnectionSplineBatchRenderer renderer = GlobalRegistry.ConnectionSplineBatchRenderer;
            if (renderer == null)
            {
                GameObject runtimeRoot = new GameObject("[ConnectionSplineBatchRenderer]"); // COLD ALLOC: GameObject[1] - bootstrap-owned shader-bent connection renderer root - owner: GameBootstrapper
                renderer = runtimeRoot.AddComponent<ConnectionSplineBatchRenderer>();
            }

            PersistRuntimeService(renderer);
            renderer.InitializeService();
            return renderer;
        }

        private static BeaconNetworkSystem EnsureBeaconNetworkServiceRegistered()
        {
            BeaconNetworkSystem beaconNetwork = GlobalRegistry.BeaconNetwork;
            if (beaconNetwork == null)
            {
                GameObject runtimeRoot = new GameObject("[BeaconNetworkSystem]"); // COLD ALLOC: GameObject[1] - bootstrap-owned beacon network root - owner: GameBootstrapper
                beaconNetwork = runtimeRoot.AddComponent<BeaconNetworkSystem>();
            }

            PersistRuntimeService(beaconNetwork);
            if (!ReferenceEquals(GlobalRegistry.BeaconNetwork, beaconNetwork))
                GlobalRegistry.RegisterBeaconNetworkRuntime(beaconNetwork);

            return beaconNetwork;
        }

        private static GlobalPhysicsStateManager EnsureGlobalPhysicsStateManagerRegistered()
        {
            GlobalPhysicsStateManager manager = GlobalRegistry.PhysicsStateManager;

            if (manager == null)
            {
                GameObject runtimeRoot = new GameObject("[GlobalPhysicsStateManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned global physics-state manager root - owner: GameBootstrapper
                manager = runtimeRoot.AddComponent<GlobalPhysicsStateManager>();
            }

            PersistRuntimeService(manager);
            manager.InitializeService();
            return manager;
        }

        private static PhysicsApplySystem EnsurePhysicsApplySystemRegistered()
        {
            PhysicsApplySystem physicsApplySystem = PhysicsApplySystem.EnsureRuntimeInstance();
            if (physicsApplySystem == null)
            {
                GameObject runtimeRoot = new GameObject("[PhysicsApplySystem]"); // COLD ALLOC: GameObject[1] - bootstrap-owned deferred physics apply root - owner: GameBootstrapper
                physicsApplySystem = runtimeRoot.AddComponent<PhysicsApplySystem>();
            }

            PersistRuntimeService(physicsApplySystem);
            physicsApplySystem.InitializeService();
            return physicsApplySystem;
        }

        private static EcosystemDirector EnsureEcosystemDirectorRegistered()
        {
            EcosystemDirector director = EcosystemDirector.ActiveRuntimeInstance;
            if (director == null)
            {
                GameObject runtimeRoot = new GameObject("[EcosystemDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned data-only ecosystem simulation owner - owner: GameBootstrapper
                director = runtimeRoot.AddComponent<EcosystemDirector>();
            }

            PersistRuntimeService(director);

            director.InitializeService();
            return director;
        }

        private static bool EnsureFaunaSimulationRegistered()
        {
            IFaunaSim registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            if (registeredFaunaSimulation != null && registeredFaunaSimulation.IsReady)
                return true;

            FaunaDirector faunaDirector = FaunaDirector.ActiveRuntimeInstance;
            if (faunaDirector != null)
                faunaDirector.InitializeService();

            registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            if (registeredFaunaSimulation != null)
                return registeredFaunaSimulation.IsReady;

            GlobalRegistry.RegisterFaunaSimulationService(DemiurgeFaunaSimulationService.Shared);
            registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            return registeredFaunaSimulation != null && registeredFaunaSimulation.IsReady;
        }

        private static InputDispatcher EnsureInputDispatcherRegistered()
        {
            if (GlobalRegistry.RegisteredInput is InputDispatcher registeredDispatcher)
            {
                registeredDispatcher.BindNativeInputManager(_bootstrapInputManager);
                return registeredDispatcher;
            }

            InputDispatcher dispatcher = InputDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[InputDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned input dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<InputDispatcher>();
            }

            dispatcher.BindNativeInputManager(_bootstrapInputManager);
            dispatcher.InitializeService();
            return dispatcher;
        }

        private static InputManager EnsureNativeInputManagerRegistered()
        {
            InputManager registeredInputManager = GlobalRegistry.NativeInputManager;
            if (_bootstrapInputManager == null)
                _bootstrapInputManager = registeredInputManager;

            if (_bootstrapInputManager == null)
                return null;

            if (!ReferenceEquals(registeredInputManager, _bootstrapInputManager))
                GlobalRegistry.RegisterNativeInputManagerRuntime(_bootstrapInputManager);

            PersistRuntimeService(_bootstrapInputManager);
            return _bootstrapInputManager;
        }

        private static PowerGridManager EnsurePowerGridServiceRegistered()
        {
            if (GlobalRegistry.PowerGrid is PowerGridManager registeredPowerGrid)
                return registeredPowerGrid;

            PowerGridManager powerGridManager = PowerGridManager.ActiveRuntimeInstance;
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
            {
                EnsureInternalFloodWaterlineRuntimeRegistered();
                return registeredConstruction;
            }

            ConstructionManager constructionManager = ConstructionManager.ActiveRuntimeInstance;
            if (constructionManager == null)
                return null;

            constructionManager.InitializeService();
            EnsureInternalFloodWaterlineRuntimeRegistered();
            return constructionManager;
        }

        private static InternalFloodWaterlineRuntime EnsureInternalFloodWaterlineRuntimeRegistered()
        {
            InternalFloodWaterlineRuntime runtime = InternalFloodWaterlineRuntime.EnsureRuntimeInstance();
            PersistRuntimeService(runtime);
            runtime.InitializeService();
            return runtime;
        }

        private static SpatialAudioManager EnsureAudioServiceRegistered()
        {
            if (GlobalRegistry.Audio is SpatialAudioManager registeredAudioService)
                return registeredAudioService;

            SpatialAudioManager sceneAudioService = ResolveAuthoredSpatialAudioManager();
            if (sceneAudioService != null)
            {
                PersistRuntimeService(sceneAudioService);
                return sceneAudioService;
            }

            return null;
        }

        private static SpatialAudioManager ResolveAuthoredSpatialAudioManager()
        {
            SpatialAudioManager activeAudioService = SpatialAudioManager.ActiveRuntimeInstance;
            if (activeAudioService != null)
                return activeAudioService;

            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper == null)
                return null;

            Transform root = bootstrapper.transform;
            _bootstrapTransformScratch.Clear();
            _bootstrapTransformScratch.Add(root);

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                if (current.TryGetComponent(out SpatialAudioManager spatialAudioManager) &&
                    spatialAudioManager != null)
                {
                    _bootstrapTransformScratch.Clear();
                    return spatialAudioManager;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapTransformScratch.Clear();
            return null;
        }

        private static bool InitializeSpatialAudioBootstrapNode()
        {
            long serviceStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                SpatialAudioManager spatialAudioManager = EnsureAudioServiceRegistered();
                if (spatialAudioManager == null)
                    return TryRegisterNoOpAudioFallback("SpatialAudioManager missing");

                spatialAudioManager.InitializeService();
                long elapsedMilliseconds =
                    (Stopwatch.GetTimestamp() - serviceStartTimestamp) * 1000L / Stopwatch.Frequency;
                if (elapsedMilliseconds > OptionalServiceTimeoutMilliseconds)
                    LogOptionalBootstrapWarning("SpatialAudioManager exceeded the optional-service bootstrap budget.");

                if (GlobalRegistry.Audio != null)
                    return true;

                return TryRegisterNoOpAudioFallback("SpatialAudioManager did not register IAudioService");
            }
            catch (Exception exception)
            {
                return TryRegisterNoOpAudioFallback(exception.Message);
            }
        }

        private static bool TryRegisterNoOpAudioFallback(string reason)
        {
            IAudioService audioService = GlobalRegistry.Audio;
            if (ReferenceEquals(audioService, NoOpAudioService.Shared))
                return true;

            if (audioService == null)
                GlobalRegistry.RegisterAudioService(NoOpAudioService.Shared);
            else
                GlobalRegistry.ReplaceAudioServiceForBootstrap(NoOpAudioService.Shared);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogOptionalBootstrapWarning("Injected NoOp audio service. Reason: " + reason);
#endif
            audioService = GlobalRegistry.Audio;
            return audioService != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogOptionalBootstrapWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[GameBootstrapper] {message}");
#endif
        }

        private static void LogBootstrapDependencyGraphFailure(BootstrapPhase phase)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Bootstrap dependency graph invalid. phase={phase}");
#endif
        }

        private static void LogBootstrapDependencyFailure(BootstrapPhase phase, BootstrapDependencyNode node)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Bootstrap dependency failed. phase={phase} node={node}");
#endif
        }

        private static void LogBootstrapHeartbeatFailure(
            BootstrapDependencyNode node,
            int waitFrames,
            double elapsedSeconds)
        {
            RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.heartbeat.timeout", ResolveBootstrapDependencyNodeName(node));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Service heartbeat timeout. node={node} frames={waitFrames} elapsed={elapsedSeconds:0.000}s");
#endif
        }

        private static void TriggerServiceEmergencyReset(BootstrapDependencyNode node)
        {
            object service = ResolveBootstrapDependencyService(node);
            if (service is RuntimeWatchdog.IEmergencyResetTarget resetTarget)
                resetTarget.ServiceEmergencyReset();
        }

        private static void LogBootstrapPhaseFailure(BootstrapPhase phase)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Bootstrap phase failed. phase={phase}");
#endif
        }

        private static bool IsHeadlessBootRequested()
        {
            return HasCommandLineArg(HectonHeadlessCommandLineArg) || HasCommandLineArg(HeadlessCommandLineArg);
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

        private static void ValidateOceanKinematicsPluginContract()
        {
            Type oceanKinematicsContract = typeof(IOceanKinematics);
            Assembly pluginsAssembly = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                AssemblyName assemblyName = assembly.GetName();
                if (assemblyName != null && string.Equals(assemblyName.Name, PluginsAssemblyName, StringComparison.Ordinal))
                {
                    pluginsAssembly = assembly;
                    break;
                }
            }

            if (pluginsAssembly == null)
                throw new InvalidOperationException("[GameBootstrapper] Hecton8.Plugins assembly missing; IOceanKinematics provider validation cannot continue.");

            Type[] pluginTypes;
            try
            {
                pluginTypes = pluginsAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                pluginTypes = exception.Types;
            }

            if (pluginTypes == null)
                throw new InvalidOperationException("[GameBootstrapper] Hecton8.Plugins type table missing; IOceanKinematics provider validation cannot continue.");

            for (int i = 0; i < pluginTypes.Length; i++)
            {
                Type pluginType = pluginTypes[i];
                if (pluginType == null ||
                    pluginType.IsInterface ||
                    pluginType.IsAbstract ||
                    !oceanKinematicsContract.IsAssignableFrom(pluginType))
                {
                    continue;
                }

                return;
            }

            throw new InvalidOperationException("[GameBootstrapper] No Hecton8.Plugins IOceanKinematics implementation found. World load is blocked.");
        }

        private static global::Hecton8.Core.HectonHardwareProfile CaptureHardwareProfile()
        {
            global::Hecton8.Optimization.HardwareProfiler.HardwareProfilerSnapshot snapshot =
                global::Hecton8.Optimization.HardwareProfiler.CaptureSystemInfoSnapshot();
            int graphicsMemoryMb = snapshot.GraphicsMemoryMegabytes;
            int systemMemoryMb = snapshot.SystemMemoryMegabytes;
            int processorCount = snapshot.ProcessorCount;
            double biosPhysicsMillisecondsPerStep = global::Hecton8.Optimization.HardwareProfiler.RunBiosPhysicsBenchmarkMillisecondsPerStep();
            global::Hecton8.Core.HectonQualityTier qualityTier = ResolveBenchmarkScalabilityTier(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                biosPhysicsMillisecondsPerStep);
            global::Hecton8.Core.MathPrecisionLevel mathPrecisionLevel = ResolveMathPrecisionLevel(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                qualityTier);

            return new global::Hecton8.Core.HectonHardwareProfile(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                qualityTier,
                biosPhysicsMillisecondsPerStep,
                snapshot.HardwareScore,
                mathPrecisionLevel);
        }

        private static global::Hecton8.Core.HectonQualityTier ResolveBenchmarkScalabilityTier(
            int graphicsMemoryMb,
            int systemMemoryMb,
            int processorCount,
            double biosPhysicsMillisecondsPerStep)
        {
            if (graphicsMemoryMb < SuspiciousGraphicsMemoryFallbackThresholdMb ||
                graphicsMemoryMb < LowTierGraphicsMemoryMb ||
                systemMemoryMb < 7000 ||
                processorCount < LowTierProcessorCount ||
                global::Hecton8.Optimization.HardwareProfiler.ShouldForceLowTier(biosPhysicsMillisecondsPerStep, graphicsMemoryMb))
            {
                return global::Hecton8.Core.HectonQualityTier.Low;
            }

            if (graphicsMemoryMb < MidTierGraphicsMemoryMb)
                return global::Hecton8.Core.HectonQualityTier.Mx350;

            if (graphicsMemoryMb < HighTierGraphicsMemoryMb || processorCount <= HighTierProcessorCount)
                return global::Hecton8.Core.HectonQualityTier.Mid;

            return processorCount > UltraTierProcessorCount && systemMemoryMb >= 32000
                ? global::Hecton8.Core.HectonQualityTier.Ultra
                : global::Hecton8.Core.HectonQualityTier.High;
        }

        private static global::Hecton8.Core.MathPrecisionLevel ResolveMathPrecisionLevel(
            int graphicsMemoryMb,
            int systemMemoryMb,
            int processorCount,
            global::Hecton8.Core.HectonQualityTier qualityTier)
        {
            if (graphicsMemoryMb < LowTierGraphicsMemoryMb || processorCount < LowTierProcessorCount)
                return global::Hecton8.Core.MathPrecisionLevel.Low;

            if (qualityTier == global::Hecton8.Core.HectonQualityTier.High ||
                qualityTier == global::Hecton8.Core.HectonQualityTier.Ultra)
            {
                return global::Hecton8.Core.MathPrecisionLevel.High;
            }

            return graphicsMemoryMb >= 4200 && systemMemoryMb >= 12000 && processorCount > 4
                ? global::Hecton8.Core.MathPrecisionLevel.High
                : global::Hecton8.Core.MathPrecisionLevel.Low;
        }

        private static void ApplyScalabilityMatrix(in HectonHardwareProfile hardwareProfile)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = ResolveTargetFrameRate(in hardwareProfile);
            QualitySettings.maximumLODLevel = ResolveMaximumLodLevel(in hardwareProfile);
            QualitySettings.streamingMipmapsMemoryBudget = ResolveStreamingMipBudgetMb(in hardwareProfile);
            QualitySettings.asyncUploadBufferSize = ResolveAsyncUploadBufferSizeMb(in hardwareProfile);
            QualitySettings.asyncUploadTimeSlice = ResolveAsyncUploadTimeSliceMs(in hardwareProfile);
            QualitySettings.asyncUploadPersistentBuffer = true;
            ConfigureJobWorkerThreads(in hardwareProfile);
        }

        private static void DisableGarbageCollectorAfterCoreReady()
        {
#if UNITY_EDITOR
            return;
#else
            if (UnityEngine.Scripting.GarbageCollector.GCMode == UnityEngine.Scripting.GarbageCollector.Mode.Disabled)
                return;

            UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Disabled;
#endif
        }

        private static int ResolveTargetFrameRate(in HectonHardwareProfile hardwareProfile)
        {
            if (HardwareTierDetector.IsQuest3Like)
                return HardwareProfileCatalog.Quest3TargetFps;
            if (HardwareTierDetector.IsSteamDeckLike)
                return HardwareProfileCatalog.SteamDeckLcdTargetFps;

            return DefaultTargetFrameRate;
        }

        private static int ResolveMaximumLodLevel(in HectonHardwareProfile hardwareProfile)
        {
            switch (hardwareProfile.QualityTier)
            {
                case HectonQualityTier.Low:
                    return 2;
                case HectonQualityTier.Mx350:
                    return 1;
                default:
                    return 0;
            }
        }

        private static float ResolveStreamingMipBudgetMb(in HectonHardwareProfile hardwareProfile)
        {
            if (HardwareTierDetector.IsQuest3Like)
                return HardwareProfileCatalog.Quest3TextureBudgetMegabytes;
            if (HardwareTierDetector.IsSteamDeckLike)
                return HardwareProfileCatalog.SteamDeckLcdTextureBudgetMegabytes;

            switch (hardwareProfile.QualityTier)
            {
                case HectonQualityTier.Ultra:
                    return 2048f;
                case HectonQualityTier.High:
                    return 1536f;
                case HectonQualityTier.Mid:
                    return 1024f;
                case HectonQualityTier.Mx350:
                    return 768f;
                default:
                    return 512f;
            }
        }

        private static int ResolveAsyncUploadBufferSizeMb(in HectonHardwareProfile hardwareProfile)
        {
            switch (hardwareProfile.QualityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return LowTierAsyncUploadBufferMb;
                case HectonQualityTier.Mid:
                    return MidTierAsyncUploadBufferMb;
                default:
                    return HighTierAsyncUploadBufferMb;
            }
        }

        private static int ResolveAsyncUploadTimeSliceMs(in HectonHardwareProfile hardwareProfile)
        {
            switch (hardwareProfile.QualityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return LowTierAsyncUploadTimeSliceMs;
                case HectonQualityTier.Mid:
                    return MidTierAsyncUploadTimeSliceMs;
                default:
                    return HighTierAsyncUploadTimeSliceMs;
            }
        }

        private static void ConfigureJobWorkerThreads(in HectonHardwareProfile hardwareProfile)
        {
            int requestedWorkerCount = ResolveJobWorkerBudget(in hardwareProfile);
            JobsUtility.JobWorkerCount = math.min(requestedWorkerCount, JobsUtility.JobWorkerMaximumCount);
        }

        private static int ResolveJobWorkerBudget(in HectonHardwareProfile hardwareProfile)
        {
            if (HardwareTierDetector.IsQuest3Like)
                return HardwareProfileCatalog.Quest3JobWorkerBudget;
            if (HardwareTierDetector.IsSteamDeckLike)
                return HardwareProfileCatalog.SteamDeckLcdJobWorkerBudget;

            return math.max(1, hardwareProfile.ProcessorCount - 1);
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

        private void MarkProjectPersistentRoot()
        {
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        private static void EnforceProjectPersistentRoot()
        {
            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper == null)
                return;

            Scene dontDestroyScene = SceneManager.GetSceneByName(DontDestroyOnLoadSceneName);
            if (!dontDestroyScene.IsValid() || !dontDestroyScene.isLoaded)
                return;

            _bootstrapSceneRootScratch.Clear();
            dontDestroyScene.GetRootGameObjects(_bootstrapSceneRootScratch);
            Transform persistentRoot = bootstrapper.transform;
            GameObject persistentRootObject = bootstrapper.gameObject;

            for (int i = _bootstrapSceneRootScratch.Count - 1; i >= 0; i--)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || root == persistentRootObject)
                    continue;

                Transform rootTransform = root.transform;
                if (rootTransform == persistentRoot || rootTransform.IsChildOf(persistentRoot))
                    continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[GameBootstrapper] Foreign DontDestroyOnLoad root destroyed. name=" + root.name);
#endif
                UnityEngine.Object.Destroy(root);
            }

            _bootstrapSceneRootScratch.Clear();
        }

        private static bool TryBuildBootstrapDependencyExecutionOrder(
            BootstrapDependencyNode[] executionOrder,
            out int executionOrderCount)
        {
            executionOrderCount = 0;
            const int nodeCount = (int)BootstrapDependencyNode.Count;
            if (executionOrder == null || executionOrder.Length < nodeCount)
                return false;

            lock (_bootstrapDependencyScratchLock)
            {
                if (!global::Hecton8.Bootstrap.BootstrapRegistryCycleValidator.TryBuildStartupExecutionOrderOrThrow(
                        _bootstrapRegistryExecutionOrderScratch,
                        out int registryOrderCount))
                {
                    return false;
                }

                if (registryOrderCount != nodeCount)
                    return false;

                for (int i = 0; i < registryOrderCount; i++)
                {
                    if (!TryResolveBootstrapDependencyNode(_bootstrapRegistryExecutionOrderScratch[i], out BootstrapDependencyNode node))
                    {
                        executionOrderCount = 0;
                        return false;
                    }

                    executionOrder[executionOrderCount++] = node;
                }

                return executionOrderCount == nodeCount;
            }
        }

        private static bool TryValidateBootstrapRegistryStartupGraph()
        {
            return global::Hecton8.Bootstrap.BootstrapRegistryCycleValidator.TryValidateStartupGraph();
        }

        private static bool TryResolveBootstrapDependencyNode(
            GlobalRegistryServiceSlot serviceSlot,
            out BootstrapDependencyNode node)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    node = BootstrapDependencyNode.SystemDispatcher;
                    return true;
                case GlobalRegistryServiceSlot.TickManager:
                    node = BootstrapDependencyNode.GameTickManager;
                    return true;
                case GlobalRegistryServiceSlot.Save:
                    node = BootstrapDependencyNode.SaveManager;
                    return true;
                case GlobalRegistryServiceSlot.ObjectPool:
                    node = BootstrapDependencyNode.ObjectPoolManager;
                    return true;
                case GlobalRegistryServiceSlot.RenderDispatcher:
                    node = BootstrapDependencyNode.RenderDispatcher;
                    return true;
                case GlobalRegistryServiceSlot.Scene:
                    node = BootstrapDependencyNode.SceneRuntimeService;
                    return true;
                case GlobalRegistryServiceSlot.InteractionSignals:
                    node = BootstrapDependencyNode.EquipmentInteractionHandler;
                    return true;
                case GlobalRegistryServiceSlot.FloatingOriginRuntime:
                    node = BootstrapDependencyNode.HectonFloatingOrigin;
                    return true;
                case GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime:
                    node = BootstrapDependencyNode.ConnectionSplineBatchRenderer;
                    return true;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    node = BootstrapDependencyNode.GlobalPhysicsStateManager;
                    return true;
                case GlobalRegistryServiceSlot.Physics:
                    node = BootstrapDependencyNode.PhysicsApplySystem;
                    return true;
                case GlobalRegistryServiceSlot.Debris:
                case GlobalRegistryServiceSlot.DebrisComputeRuntime:
                    node = BootstrapDependencyNode.DebrisManager;
                    return true;
                case GlobalRegistryServiceSlot.Environment:
                    node = BootstrapDependencyNode.EnvironmentRuntimeContextService;
                    return true;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    node = BootstrapDependencyNode.OceanKinematicsRuntimeService;
                    return true;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    node = BootstrapDependencyNode.EcosystemDirector;
                    return true;
                case GlobalRegistryServiceSlot.FaunaSimulation:
                    node = BootstrapDependencyNode.FaunaSimulation;
                    return true;
                case GlobalRegistryServiceSlot.Audio:
                    node = BootstrapDependencyNode.SpatialAudioManager;
                    return true;
                case GlobalRegistryServiceSlot.PowerGrid:
                    node = BootstrapDependencyNode.PowerGridManager;
                    return true;
                case GlobalRegistryServiceSlot.Logistics:
                    node = BootstrapDependencyNode.ConstructionManager;
                    return true;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    node = BootstrapDependencyNode.NativeInputManager;
                    return true;
                case GlobalRegistryServiceSlot.Input:
                    node = BootstrapDependencyNode.InputDispatcher;
                    return true;
                case GlobalRegistryServiceSlot.Player:
                    node = BootstrapDependencyNode.PlayerRuntimeContextService;
                    return true;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    node = BootstrapDependencyNode.PlayerInventoryManager;
                    return true;
                case GlobalRegistryServiceSlot.PlayerSensory:
                    node = BootstrapDependencyNode.PlayerSensoryManager;
                    return true;
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime:
                    node = BootstrapDependencyNode.BeaconNetworkSystem;
                    return true;
                case GlobalRegistryServiceSlot.ModWorldPersistenceRuntime:
                    node = BootstrapDependencyNode.ModWorldPersistenceManager;
                    return true;
                default:
                    node = default;
                    return false;
            }
        }

        private static string ResolveBootstrapDependencyNodeName(BootstrapDependencyNode node)
        {
            int index = (int)node;
            return index >= 0 && index < _bootstrapDependencyNodeNames.Length
                ? _bootstrapDependencyNodeNames[index]
                : "Unknown";
        }

        private static void HandleSceneLoadedGuard(Scene scene, LoadSceneMode mode)
        {
#if UNITY_INCLUDE_TESTS
            if (_isUnityTestRunnerProcess)
                return;
#endif
            if (!Application.isPlaying)
                return;

            if (_isBootstrapComplete)
            {
                EnsureExtendedRegistryCoverageForActiveScene();
                if (RequiresGameplaySceneActivation(scene))
                    RequestSceneActivation();

                BootstrapBiosErrorOverlay.Hide();
                return;
            }

            TryRecoverEntryVector(scene, true);
        }

        private static bool TryRecoverEntryVector(Scene scene, bool allowRecovery)
        {
#if UNITY_INCLUDE_TESTS
            if (_isUnityTestRunnerProcess)
                return IsBootstrapScene(scene);
#endif
            if (IsBootstrapScene(scene))
            {
                _entryRecoveryIssued = false;
                BootstrapBiosErrorOverlay.Hide();
                return true;
            }

            BootstrapBiosErrorOverlay.Show(BuildBiosErrorMessage(
                string.IsNullOrEmpty(scene.name) ? "<unnamed>" : scene.name,
                scene.buildIndex));

            if (!allowRecovery || _entryRecoveryIssued)
                return false;

            _entryRecoveryIssued = true;
            GameStartContextHolder.Reset();
            SceneManager.LoadScene(BootstrapSceneName);
            return false;
        }

        private async Awaitable<bool> ExecuteSceneReadinessGatesAsync(CancellationToken ownerToken)
        {
            if (_sceneActivationStarted)
                return _debugSceneActivationCompleted;

            _sceneActivationStarted = true;
            _debugSceneActivationCompleted = false;
            BootstrapState.PublishGameReady(false);
            BootstrapState.PublishBootstrapPresence(true);

            Scene activeScene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            if (RejectDirtyEditorSceneAndReloadFromDisk(activeScene))
                return false;
#endif
            SceneInstantiationGate.ActiveRuntime?.BeginSceneLoad(activeScene.name);
            ResolveSceneActivationReferences(activeScene);
            DisablePlayer();
            ApplyShippingSceneCleanup(activeScene);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                ownerToken,
                destroyCancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(bootstrapTimeout));
            CancellationToken ct = cts.Token;

            try
            {
                SetSceneActivationStep("Step 1: Verifying Singletons");
                if (!VerifySingletons())
                {
                    FailSceneActivation("Critical singletons missing. Bootstrap aborted.");
                    return false;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 2: Pool Warmup");
                await WarmupPoolsAsync(ct);

                SetSceneActivationStep("Step 3: World Generation");
                WriteBootStateRecord(BootStateMarker.WorldGen, BootstrapPhase.SceneActivate, GlobalRegistryServiceSlot.WorldGen);
                StartWorldGeneration();
                if (worldGenWaitTime > 0f)
                    await Awaitable.WaitForSecondsAsync(worldGenWaitTime, cancellationToken: ct);
                else
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);

                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 4: Save/Load");
                await LoadOrNewGameAsync();
                ResolveSceneActivationReferences(activeScene);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 5: World-Ready Check");
                await WaitForWorldReadyAsync(ct);

                SetSceneActivationStep("Step 6: Ground-Ready Check");
                await WaitForGroundReadyAsync(ct);

                SetSceneActivationStep("Step 7: Player Spawn");
                await SpawnPlayerAsync(ct);
                SceneInstantiationGate.ActiveRuntime?.MarkPlayerInstantiated(playerObject);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8: Runtime World Prime");
                await PrimeRuntimeWorldAsync(ct);
                SceneInstantiationGate.ActiveRuntime?.MarkWorldPrimed();
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.5: Cold Cleanup + Memory Snapshot");
                await RunColdCleanupAndCaptureMemorySnapshotAsync(ct);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.75: Resident World Prefab Gate");
                if (!await WaitForResidentWorldPrefabPoolsReadyAsync(ct))
                    return false;
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.9: Scene Gate Verification");
                await WaitForSceneInstantiationGateAsync(ct);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.95: Scene Graph Guard");
                if (!TryValidateSceneRootBudget(activeScene, "game-bootstrapper-scene-activation"))
                {
                    FailSceneActivation("Scene graph corruption guard aborted activation.");
                    return false;
                }

                ActivatePlayer();

                SetSceneActivationStep("Complete");
                _debugSceneActivationCompleted = true;
                BootstrapState.PublishGameReady(true);
                BootstrapState.PublishBootstrapPresence(false);
                RaiseGameReadyEvent();
                return true;
            }
            catch (OperationCanceledException)
            {
                BootstrapState.PublishGameReady(false);
                BootstrapState.PublishBootstrapPresence(false);
                if (this == null || destroyCancellationToken.IsCancellationRequested)
                    return false;

                FailSceneActivation("Bootstrap timed out after " + bootstrapTimeout + "s. Last step: " + _debugSceneActivationStep);
                return false;
            }
            catch (Exception exception)
            {
                BootstrapState.PublishGameReady(false);
                BootstrapState.PublishBootstrapPresence(false);
                if (this == null)
                    return false;

                FailSceneActivation("Bootstrap failed at [" + _debugSceneActivationStep + "]: " + exception.Message);
                HandleFatalBootstrapException(_debugSceneActivationStep, exception);
                return false;
            }
        }

        private void ResolveSceneActivationReferences(Scene scene)
        {
            if (playerObject == null)
                playerObject = BootstrapState.CurrentPlayerObject;

            if (playerObject == null)
            {
                TryResolveSceneTaggedObject(scene, "Player", out GameObject taggedPlayer);
                if (taggedPlayer != null && !IsTemporaryRuntimeShellObject(taggedPlayer))
                    playerObject = taggedPlayer;
            }

            if (playerSpawner == null)
            {
                TryResolveSceneComponent(scene, includeInactive: true, out HectonPlayerSpawner spawner);
                if (spawner != null && !IsTemporaryRuntimeShellObject(spawner.gameObject))
                    playerSpawner = spawner;
            }

            if (playerRigidbody == null && playerObject != null)
                playerRigidbody = playerObject.GetComponent<Rigidbody>();

            if (playerController == null && playerObject != null)
                playerController = playerObject.GetComponent<MonoBehaviour>();

            PublishPlayerRuntimeReference();
        }

        private bool VerifySingletons()
        {
            bool allCritical = true;

            if (GlobalRegistry.Dispatcher == null)
            {
                Debug.LogError("[GameBootstrapper] SystemDispatcher not found.");
                allCritical = false;
            }

            if (GlobalRegistry.ObjectPool == null)
            {
                Debug.LogError("[GameBootstrapper] ObjectPoolManager not found.");
                allCritical = false;
            }

            if (PrefabRegistry.ActiveRuntimeInstance == null)
            {
                Debug.LogError("[GameBootstrapper] PrefabRegistry not found.");
                allCritical = false;
            }

            if (GlobalRegistry.SaveRuntime == null)
            {
                Debug.LogError("[GameBootstrapper] SaveManager not found.");
                allCritical = false;
            }

            if (GlobalRegistry.WorldState == null)
                Debug.LogWarning("[GameBootstrapper] WorldStateManager not found.");

            if (GlobalRegistry.ConstructionRuntime == null)
                Debug.LogWarning("[GameBootstrapper] ConstructionManager not found.");

            return allCritical;
        }

        private async Awaitable WarmupPoolsAsync(CancellationToken ct)
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null || warmupEntries == null)
                return;

            for (int i = 0, entryCount = warmupEntries.Count; i < entryCount; i++)
            {
                WarmupEntry entry = warmupEntries[i];
                if (entry.prefab == null || entry.count <= 0)
                    continue;

                string label = string.IsNullOrEmpty(entry.label) ? entry.prefab.name : entry.label;
                for (int created = 0; created < entry.count;)
                {
                    int batch = Mathf.Min(WarmupBatchSize, entry.count - created);
                    pool.Warmup(entry.prefab, batch);
                    created += batch;
                    SetSceneActivationStep("Warming Pool: " + label + " (" + created + "/" + entry.count + ")");
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        private void StartWorldGeneration()
        {
            ITerrainProvider terrainProvider = GlobalRegistry.Terrain;
            if (terrainProvider != null && terrainProvider.IsAvailable)
                return;

            global::HectonWorldGenerator legacyWorldGenerator =
                GlobalRegistry.WorldSeedProvider as global::HectonWorldGenerator;
            if (legacyWorldGenerator != null && !IsTemporaryRuntimeShellObject(legacyWorldGenerator.gameObject))
                return;
        }

        private async Awaitable LoadOrNewGameAsync()
        {
            SaveManager save = GlobalRegistry.SaveRuntime;
            if (save == null)
            {
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            GameStartContext context;
            if (!GameStartContextHolder.TryGetCurrentOrRestore(out context))
                context = GameStartContext.CreateNewGame();

            GameStartContextHolder.ClearPersistedHandoff();
            if (forceNewGame)
                context = GameStartContext.CreateNewGame();

            GameStartContextHolder.Current = context;
            if (context.StartMode == GameStartMode.NewGame || string.IsNullOrEmpty(context.TargetSaveSlot))
            {
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            if (!save.SaveExists(context.TargetSaveSlot))
            {
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            try
            {
                await save.LoadGameAsync(context.TargetSaveSlot);
                if (!save.LastOperationSucceeded)
                {
                    _isLoadingSave = false;
                    InitNewGame();
                    return;
                }

                _isLoadingSave = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GameBootstrapper] Save load failed: " + exception.Message);
                _isLoadingSave = false;
                InitNewGame();
            }
        }

        private void InitNewGame()
        {
            WorldStateManager worldStateManager = GlobalRegistry.WorldState;
            if (worldStateManager != null)
                worldStateManager.ClearAll();

            ConstructionManager constructionManager = GlobalRegistry.ConstructionRuntime;
            if (constructionManager != null)
                constructionManager.ClearAllModules();
        }

        private async Awaitable WaitForWorldReadyAsync(CancellationToken ct)
        {
            ScavengePopulator populator = GlobalRegistry.ScavengePopulator;
            if (populator == null)
                return;

            int lastPendingCount = int.MaxValue;
            int stagnantPollCount = 0;
            while (populator.PendingSpawnCount > WorldReadyThreshold)
            {
                int pendingCount = populator.PendingSpawnCount;
                if (pendingCount < lastPendingCount)
                {
                    lastPendingCount = pendingCount;
                    stagnantPollCount = 0;
                }
                else if (++stagnantPollCount >= WorldReadyStagnationPollLimit)
                {
                    Debug.LogWarning("[GameBootstrapper] World-ready queue stalled. Continuing bootstrap.");
                    return;
                }

                await Awaitable.WaitForSecondsAsync(WorldReadyPollIntervalSec, cancellationToken: ct);
                ct.ThrowIfCancellationRequested();
            }
        }

        private async Awaitable WaitForGroundReadyAsync(CancellationToken ct)
        {
            if (!_isLoadingSave || playerObject == null)
                return;

            Vector3 playerPosition = playerObject.transform.position;
            float elapsed = 0f;
            while (elapsed < groundReadyTimeout)
            {
                ct.ThrowIfCancellationRequested();
                Vector3 rayOrigin = playerPosition + Vector3.up * GroundCheckRayOffset;
                Ray ray = new Ray(rayOrigin, Vector3.down);
                int groundMask = groundReadyLayerMask.value != 0
                    ? groundReadyLayerMask.value
                    : HectonLayerMasks.SeamProbeLayerMask;

                if (UnityEngine.Physics.RaycastNonAlloc(
                        ray,
                        _groundCheckHits,
                        GroundCheckRayLength,
                        groundMask,
                        QueryTriggerInteraction.Ignore) > 0)
                {
                    return;
                }

                await Awaitable.WaitForSecondsAsync(GroundCheckPollIntervalSec, cancellationToken: ct);
                elapsed += GroundCheckPollIntervalSec;
            }

            Debug.LogWarning("[GameBootstrapper] Ground-ready timed out. Activating player without collider confirmation.");
        }

        private async Awaitable SpawnPlayerAsync(CancellationToken ct)
        {
            if (_isLoadingSave)
                return;

            if (playerSpawner != null && playerSpawner.TryGetComponent(out HectonPlayerSpawner spawner))
            {
                await spawner.SpawnPlayerAsync(ct);
                ResolveSceneActivationReferences(SceneManager.GetActiveScene());
                return;
            }

            if (playerObject != null)
            {
                playerObject.transform.position = fallbackSpawnPosition;
                PublishPlayerRuntimeReference();
                return;
            }

            Debug.LogWarning("[GameBootstrapper] No player spawner or owned player reference is available.");
        }

        private async Awaitable PrimeRuntimeWorldAsync(CancellationToken ct)
        {
            if (!prewarmProceduralScatterBeforePlayerActivation)
                return;

            if (!TryResolveProductionScatterDirector(out _worldProceduralScatterDirector))
                return;

            int passCount = Mathf.Clamp(scatterBootstrapPrimePasses, 1, 4);
            if (_worldProceduralScatterDirector.TryPrewarmBootstrapSamplingPipeline())
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);

            for (int i = 0; i < passCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (!_worldProceduralScatterDirector.TryPrimeBootstrapScatterPass())
                    return;

                if (!_worldProceduralScatterDirector.HasBootstrapPrimeWork)
                    return;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }
        }

        private async Awaitable RunColdCleanupAndCaptureMemorySnapshotAsync(CancellationToken ct)
        {
            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor != null)
                governor.ForceDrainPendingReleaseQueue();

            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            ct.ThrowIfCancellationRequested();

            VRAMPressureMonitor pressureMonitor = GlobalRegistry.VRAMPressure;
            if (pressureMonitor != null)
                pressureMonitor.ForceImmediateSampleAndResponse();

            CaptureStartupMemorySnapshot();
            float totalVramMb = 0f;
            VRAMMonitor vramMonitor = GlobalRegistry.VRAMMonitor;
            if (vramMonitor != null)
                totalVramMb = vramMonitor.TotalVRAMBytes / BytesPerMegabyte;

            SceneInstantiationGate.ActiveRuntime?.CaptureMemorySnapshot(
                _debugStartupTextureMemoryMb,
                _debugStartupReservedMemoryMb,
                totalVramMb);
        }

        private async Awaitable WaitForSceneInstantiationGateAsync(CancellationToken ct)
        {
            SceneInstantiationGate gate = SceneInstantiationGate.ActiveRuntime;
            if (gate != null)
                await gate.WaitForOpenAsync(ct);
        }

        private async Awaitable<bool> WaitForResidentWorldPrefabPoolsReadyAsync(CancellationToken ct)
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
            {
                FailSceneActivation("PersistentWorldRegistry missing.");
                return false;
            }

            while (Application.isPlaying && BootstrapState.HasActiveInstance && !registry.AreResidentWorldPrefabPoolsReady())
            {
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                ct.ThrowIfCancellationRequested();
            }

            return true;
        }

        private void DisablePlayer()
        {
            PublishPlayerRuntimeReference();
            if (playerRigidbody == null && playerObject != null)
                playerRigidbody = playerObject.GetComponent<Rigidbody>();

            if (playerRigidbody != null)
                playerRigidbody.isKinematic = true;

            if (playerObject != null && playerObject.activeSelf)
                playerObject.SetActive(false);

            if (playerController != null)
                playerController.enabled = false;
        }

        private void ActivatePlayer()
        {
            PublishPlayerRuntimeReference();
            if (playerObject != null)
                playerObject.SetActive(true);

            if (playerRigidbody != null)
                playerRigidbody.isKinematic = false;

            if (playerController != null)
                playerController.enabled = true;
        }

        private void PublishPlayerRuntimeReference()
        {
            if (IsTemporaryRuntimeShellObject(playerObject))
                playerObject = null;

            if (playerObject == null && playerRigidbody != null && !IsTemporaryRuntimeShellObject(playerRigidbody.gameObject))
                playerObject = playerRigidbody.gameObject;

            if (playerObject == null && playerController != null && !IsTemporaryRuntimeShellObject(playerController.gameObject))
                playerObject = playerController.gameObject;

            Hecton8.Meta.MetaRuntimeInstaller.EnsureRuntimeSystems();
            Hecton8.Economy.EconomyRuntimeInstaller.EnsureRuntimeSystems();
            Hecton8.Ecosystem.EcosystemRuntimeInstaller.EnsureRuntimeSystems();
            Hecton8.PDA.PDARuntimeInstaller.EnsurePlayerSystems(playerObject);
            Hecton8.Progression.ProgressionRuntimeInstaller.EnsurePlayerSystems(playerObject);
            Hecton8.Narrative.NarrativeRuntimeInstaller.EnsurePlayerSystems(playerObject);
            Hecton8.Audio.AtmosphericAudioRuntimeInstaller.EnsurePlayerSystems(playerObject);
            BootstrapState.PublishCurrentPlayerObject(playerObject);
        }

        private void ApplyShippingSceneCleanup(Scene scene)
        {
            int suppressedCount = WorldShippingContentFilter.DeactivateSuppressedSceneObjects(
                scene,
                _shippingCleanupRootObjects,
                _shippingCleanupTraversalStack);

            if (suppressedCount > 0)
                LogSceneActivation("[ShippingCleanup] Deactivated " + suppressedCount + " dev/trial scene objects.");
        }

        private void CaptureStartupMemorySnapshot()
        {
            TryReadMemoryMetricMegabytes(_TextureMemoryCandidates, out _debugStartupTextureMemoryMb, out _debugStartupTextureMetric);
            TryReadMemoryMetricMegabytes(_TotalReservedMemoryCandidates, out _debugStartupReservedMemoryMb, out _debugStartupReservedMetric);
        }

        private static bool TryReadMemoryMetricMegabytes(
            string[] candidates,
            out float megabytes,
            out string resolvedMetric)
        {
            megabytes = 0f;
            resolvedMetric = "Unresolved";

            lock (_profilerRecorderHandleScratch)
            {
                _profilerRecorderHandleScratch.Clear();
                ProfilerRecorderHandle.GetAvailable(_profilerRecorderHandleScratch);

                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    string candidate = candidates[candidateIndex];
                    for (int handleIndex = 0; handleIndex < _profilerRecorderHandleScratch.Count; handleIndex++)
                    {
                        ProfilerRecorderDescription description =
                            ProfilerRecorderHandle.GetDescription(_profilerRecorderHandleScratch[handleIndex]);

                        if (!string.Equals(description.Name, candidate, StringComparison.OrdinalIgnoreCase))
                            continue;

                        ProfilerRecorder recorder = default;
                        try
                        {
                            recorder = ProfilerRecorder.StartNew(
                                description.Category,
                                description.Name,
                                1,
                                ProfilerRecorderOptions.Default);

                            if (!recorder.Valid)
                                continue;

                            megabytes = recorder.LastValue / BytesPerMegabyte;
                            resolvedMetric = description.Name;
                            return true;
                        }
                        catch (ArgumentException)
                        {
                        }
                        finally
                        {
                            if (recorder.Valid)
                                recorder.Dispose();
                        }
                    }
                }

                _profilerRecorderHandleScratch.Clear();
            }

            return false;
        }

        private static bool IsTemporaryRuntimeShellObject(GameObject candidate)
        {
            if (candidate == null)
                return false;

            Transform current = candidate.transform;
            while (current != null)
            {
                if (IsTemporaryRuntimeShellName(current.name))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static bool IsTemporaryRuntimeShellName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name.StartsWith("__", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("temp", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("_trial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_staging", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_preview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_smoke", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryResolveProductionScatterDirector(out WorldProceduralScatterDirector director)
        {
            director = WorldProceduralScatterDirector.ActiveRuntimeInstance;
            if (director != null && !IsTemporaryRuntimeShellObject(director.gameObject))
                return true;

            int registeredDirectorCount = WorldProceduralScatterDirector.RegisteredDirectorCount;
            for (int i = 0; i < registeredDirectorCount; i++)
            {
                WorldProceduralScatterDirector candidate = WorldProceduralScatterDirector.GetRegisteredDirectorAt(i);
                if (candidate == null || IsTemporaryRuntimeShellObject(candidate.gameObject))
                    continue;

                director = candidate;
                return true;
            }

            director = null;
            return false;
        }

        private void FailSceneActivation(string error)
        {
            Debug.LogError("[GameBootstrapper] " + error);
            RaiseBootstrapFailedEvent(error);
        }

        private void LogSceneActivation(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent("game-bootstrapper.scene", message);
#endif
            if (verboseSceneActivationLogging)
                Debug.Log("[GameBootstrapper] " + message);
        }

        private void SetSceneActivationStep(string step)
        {
            _debugSceneActivationStep = step;
            LogSceneActivation(step);
        }

#if UNITY_EDITOR
        private static bool RejectDirtyEditorSceneAndReloadFromDisk(Scene scene)
        {
            if (!Application.isEditor || !scene.IsValid() || !scene.isDirty)
                return false;

            string scenePath = scene.path;
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[GameBootstrapper] Dirty editor scene rejected, but scene has no disk path.");
                return true;
            }

            Debug.LogError("[GameBootstrapper] Dirty editor scene rejected; reloading from disk: " + scenePath);
            _pendingDirtySceneReloadPath = scenePath;
            UnityEditor.EditorApplication.delayCall -= ProcessDirtySceneReloadFromDisk;
            UnityEditor.EditorApplication.delayCall += ProcessDirtySceneReloadFromDisk;
            return true;
        }

        private static void ProcessDirtySceneReloadFromDisk()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.EditorApplication.ExitPlaymode();
                UnityEditor.EditorApplication.delayCall -= ProcessDirtySceneReloadFromDisk;
                UnityEditor.EditorApplication.delayCall += ProcessDirtySceneReloadFromDisk;
                return;
            }

            string scenePath = _pendingDirtySceneReloadPath;
            _pendingDirtySceneReloadPath = null;
            if (!string.IsNullOrEmpty(scenePath))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
#endif

        private void StartBackgroundDomainHandshake()
        {
            if (Volatile.Read(ref _backgroundDomainHandshakeState) != BackgroundDomainHandshakeIdle)
                return;

            _backgroundDomainHandshakePath = HectonPersistentPathPolicy.CombineDirectory("Telemetry");
            _backgroundDomainHandshakeError = null;
            string capturedPath = _backgroundDomainHandshakePath;
            Volatile.Write(ref _backgroundDomainHandshakeState, BackgroundDomainHandshakeRunning);
            _ = RunBackgroundDomainHandshakeAsync(capturedPath);
        }

        private async Awaitable RunBackgroundDomainHandshakeAsync(string telemetryPath)
        {
            string error = null;
            int finalState = BackgroundDomainHandshakeComplete;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                PrepareBackgroundDomainHandshake(telemetryPath);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                finalState = BackgroundDomainHandshakeFailed;
            }

            await Awaitable.MainThreadAsync();
            _backgroundDomainHandshakeError = error;
            Volatile.Write(ref _backgroundDomainHandshakeState, finalState);
        }

        private static void PrepareBackgroundDomainHandshake(string telemetryPath)
        {
            if (string.IsNullOrEmpty(telemetryPath))
                return;

            Directory.CreateDirectory(telemetryPath);
        }

        private async Awaitable<bool> JoinBackgroundDomainHandshakeAsync(CancellationToken ct)
        {
            int state = Volatile.Read(ref _backgroundDomainHandshakeState);
            if (state == BackgroundDomainHandshakeIdle)
                return true;

            while (state == BackgroundDomainHandshakeRunning)
            {
                ct.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                state = Volatile.Read(ref _backgroundDomainHandshakeState);
            }

            if (state == BackgroundDomainHandshakeFailed)
            {
                Debug.LogError("[GameBootstrapper] Background domain handshake failed: " + _backgroundDomainHandshakeError);
                return false;
            }

            return true;
        }

        private static void ApplyMemoryGate(in HectonHardwareProfile hardwareProfile)
        {
            if (hardwareProfile.SystemMemoryMegabytes < LowMemorySystemThresholdMb ||
                hardwareProfile.GraphicsMemoryMegabytes <= LowMemoryVramThresholdMb)
            {
                GlobalRegistry.FlagFallbackLowMemoryProfile();
            }
        }

        private static void InspectPreviousBootState()
        {
            string path = HectonPersistentPathPolicy.CombineFile(BootStateFileName);
            if (!File.Exists(path))
                return;

            Span<byte> record = stackalloc byte[BootStateRecordBytes];
            int bytesRead = 0;
            try
            {
                using FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    BootStateRecordBytes,
                    FileOptions.SequentialScan);

                while (bytesRead < BootStateRecordBytes)
                {
                    int read = stream.Read(record.Slice(bytesRead, BootStateRecordBytes - bytesRead));
                    if (read <= 0)
                        return;

                    bytesRead += read;
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            uint magic = ReadUInt32(record, 0);
            ushort version = ReadUInt16(record, 4);
            if (magic != BootStateMagic || version != BootStateVersion)
                return;

            BootStateMarker marker = (BootStateMarker)record[6];
            if (marker == BootStateMarker.Complete)
                return;

            _bootStateSafeModeRequested = true;
            GlobalRegistry.RequestSafeModeBoot();
        }

        private static unsafe void WriteBootStateRecord(
            BootStateMarker marker,
            BootstrapPhase phase,
            GlobalRegistryServiceSlot serviceSlot)
        {
            string absolutePath = HectonPersistentPathPolicy.CombineFile(BootStateFileName);
            HectonPersistentPathPolicy.EnsureParentDirectory(absolutePath);
            NativeArray<byte> record = new NativeArray<byte>(
                BootStateRecordBytes,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[32] - boot crash recovery state - owner: GameBootstrapper
            try
            {
                byte* data = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(record);
                WriteUInt32(data, 0, BootStateMagic);
                WriteUInt16(data, 4, BootStateVersion);
                data[6] = (byte)marker;
                data[7] = (byte)phase;
                data[8] = serviceSlot == GlobalRegistryServiceSlot.Unknown ? byte.MaxValue : (byte)serviceSlot;
                WriteUInt32(data, 12, _registryCoreReadyChecksum);
                WriteUInt32(data, 16, GlobalRegistry.ActiveServiceTypeHash);
                WriteUInt64(data, 20, unchecked((ulong)DateTime.UtcNow.Ticks));
                data[28] = _bootStateSafeModeRequested ? (byte)1 : (byte)0;
                data[29] = (byte)GlobalRegistry.ActiveBootProfile;
                AsyncWriteManager.WriteAll(absolutePath, data, BootStateRecordBytes, out _);
            }
            finally
            {
                record.Dispose();
            }
        }

        private static uint CalculateRegistryActiveServiceTypeHash()
        {
            uint hash = GlobalRegistry.CalculateActiveServiceTypeFnv1a();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hash == 0u)
                Debug.LogError("[GameBootstrapper] BIOS integrity checksum resolved to zero.");
#endif
            return hash;
        }

        private void ShutdownServicesInReverseBootstrapOrder()
        {
            if (_bootstrapExecutionOrderCount <= 0)
                TryBuildBootstrapDependencyExecutionOrder(_bootstrapExecutionOrder, out _bootstrapExecutionOrderCount);

            for (int index = _bootstrapExecutionOrderCount - 1; index >= 0; index--)
            {
                GlobalRegistryServiceSlot slot = ResolveRegistrySlotForBootstrapNode(_bootstrapExecutionOrder[index]);
                if (slot != GlobalRegistryServiceSlot.Unknown)
                    GlobalRegistry.ShutdownRegisteredServiceSlot(slot);
            }

            GlobalRegistry.ShutdownRegisteredServicesInReverseSlotOrder();
        }

        private static GlobalRegistryServiceSlot ResolveRegistrySlotForBootstrapNode(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher: return GlobalRegistryServiceSlot.Dispatcher;
                case BootstrapDependencyNode.GameTickManager: return GlobalRegistryServiceSlot.TickManager;
                case BootstrapDependencyNode.SaveManager: return GlobalRegistryServiceSlot.Save;
                case BootstrapDependencyNode.ObjectPoolManager: return GlobalRegistryServiceSlot.ObjectPool;
                case BootstrapDependencyNode.RenderDispatcher: return GlobalRegistryServiceSlot.RenderDispatcher;
                case BootstrapDependencyNode.SceneRuntimeService: return GlobalRegistryServiceSlot.Scene;
                case BootstrapDependencyNode.EquipmentInteractionHandler: return GlobalRegistryServiceSlot.InteractionSignals;
                case BootstrapDependencyNode.HectonFloatingOrigin: return GlobalRegistryServiceSlot.FloatingOriginRuntime;
                case BootstrapDependencyNode.GlobalPhysicsStateManager: return GlobalRegistryServiceSlot.PhysicsStateManager;
                case BootstrapDependencyNode.PhysicsApplySystem: return GlobalRegistryServiceSlot.Physics;
                case BootstrapDependencyNode.DebrisManager: return GlobalRegistryServiceSlot.DebrisComputeRuntime;
                case BootstrapDependencyNode.EnvironmentRuntimeContextService: return GlobalRegistryServiceSlot.Environment;
                case BootstrapDependencyNode.OceanKinematicsRuntimeService: return GlobalRegistryServiceSlot.OceanKinematics;
                case BootstrapDependencyNode.EcosystemDirector: return GlobalRegistryServiceSlot.EcosystemDirector;
                case BootstrapDependencyNode.FaunaSimulation: return GlobalRegistryServiceSlot.FaunaSimulation;
                case BootstrapDependencyNode.SpatialAudioManager: return GlobalRegistryServiceSlot.Audio;
                case BootstrapDependencyNode.NativeInputManager: return GlobalRegistryServiceSlot.NativeInputManagerRuntime;
                case BootstrapDependencyNode.InputDispatcher: return GlobalRegistryServiceSlot.Input;
                case BootstrapDependencyNode.PlayerRuntimeContextService: return GlobalRegistryServiceSlot.Player;
                case BootstrapDependencyNode.PlayerInventoryManager: return GlobalRegistryServiceSlot.PlayerInventory;
                case BootstrapDependencyNode.PlayerSensoryManager: return GlobalRegistryServiceSlot.PlayerSensory;
                case BootstrapDependencyNode.PowerGridManager: return GlobalRegistryServiceSlot.PowerGrid;
                case BootstrapDependencyNode.ConstructionManager: return GlobalRegistryServiceSlot.Logistics;
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer: return GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime;
                case BootstrapDependencyNode.BeaconNetworkSystem: return GlobalRegistryServiceSlot.BeaconNetworkRuntime;
                case BootstrapDependencyNode.ModWorldPersistenceManager: return GlobalRegistryServiceSlot.ModWorldPersistenceRuntime;
                default: return GlobalRegistryServiceSlot.Unknown;
            }
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        {
            return (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static unsafe void WriteUInt16(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteUInt32(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteUInt64(byte* data, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                data[offset + i] = (byte)(value >> (i * 8));
        }

#if UNITY_INCLUDE_TESTS
        private static bool ResolveUnityTestRunnerProcess()
        {
            string[] args = System.Environment.GetCommandLineArgs(); // COLD ALLOC: string[] — Unity Test Framework process probe — owner: GameBootstrapper
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "-runTests", StringComparison.Ordinal) ||
                    string.Equals(arg, "-runEditorTests", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        private static void EnsureExtendedRegistryCoverageForActiveScene()
        {
            TryEnsureThermodynamicsRegistryCoverage();
            TryEnsureLogisticsRegistryCoverage();
            TryEnsureWorldGenRegistryCoverage();
            TryEnsureEncounterDirectorRegistryCoverage();
            TryEnsureQuestRegistryCoverage();
            TryEnsureProceduralSwayRegistryCoverage();
        }

        private static void TryEnsureThermodynamicsRegistryCoverage()
        {
            if (GlobalRegistry.ThermodynamicsService != null)
                return;

            AbyssalThermalManager manager = GlobalRegistry.Thermodynamics;
            if (manager != null)
                GlobalRegistry.RegisterThermodynamicsRuntime(manager);
        }

        private static void TryEnsureLogisticsRegistryCoverage()
        {
            if (GlobalRegistry.Logistics != null)
                return;

            ConstructionManager manager = ConstructionManager.ActiveRuntimeInstance;
            if (manager != null)
                GlobalRegistry.RegisterLogisticsService(manager);
        }

        private static void TryEnsureWorldGenRegistryCoverage()
        {
            if (GlobalRegistry.WorldGen != null)
                return;

            WorldProceduralScatterDirector director = WorldProceduralScatterDirector.ActiveRuntimeInstance;
            if (director != null)
                GlobalRegistry.RegisterWorldGenService(director);
        }

        private static void TryEnsureEncounterDirectorRegistryCoverage()
        {
            if (GlobalRegistry.EncounterDirector != null)
                return;

            HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;
            if (director != null)
                GlobalRegistry.RegisterEncounterDirectorService(director);
        }

        private static void TryEnsureQuestRegistryCoverage()
        {
            if (GlobalRegistry.QuestSystem != null)
                return;

            QuestManager questManager = QuestManager.ActiveRuntimeInstance;
            if (questManager != null)
                GlobalRegistry.RegisterQuestRuntime(questManager);
        }

        private static void TryEnsureProceduralSwayRegistryCoverage()
        {
            if (GlobalRegistry.ProceduralSwayDirector != null)
                return;

            FloraInteractionManager manager = FloraInteractionManager.ActiveRuntimeInstance;
            if (manager != null)
                GlobalRegistry.RegisterProceduralSwayDirector(manager);
        }

        private static void EnsureBootstrapAudioListener(Scene bootstrapScene)
        {
            if (HasActiveAudioListener(bootstrapScene))
                return;

            GameObject listenerObject = new GameObject(BootstrapAudioListenerRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-only audio listener before menu handoff - owner: GameBootstrapper
            if (bootstrapScene.IsValid())
                SceneManager.MoveGameObjectToScene(listenerObject, bootstrapScene);

            listenerObject.AddComponent<AudioListener>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.audio", "created bootstrap-only listener");
#endif
        }

        private static bool HasActiveAudioListener(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || !root.activeInHierarchy)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if (currentObject.activeInHierarchy &&
                    currentObject.TryGetComponent(out AudioListener listener) &&
                    listener != null &&
                    listener.enabled)
                {
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return true;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            return false;
        }

        private static async Awaitable WaitForJobCompletionAsync(JobHandle handle, CancellationToken ct)
        {
            int waitFrames = 0;
            long waitStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                while (!handle.IsCompleted)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapJobWaitWatchdogSeconds, out double elapsedSeconds))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError($"[GameBootstrapper] Job wait watchdog tripped after {waitFrames} frames ({elapsedSeconds:0.000}s). Forcing completion as cleanup barrier.");
#endif
                        break;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
        }

        private static void HandleFatalBootstrapException(string phaseName, Exception exception)
        {
            if (exception == null)
                return;

            string crashMessage = BuildFatalBootstrapMessage(phaseName, exception);
            WriteFatalBootstrapLog(crashMessage);
            BootstrapBiosErrorOverlay.Show(BuildFatalBootOverlayMessage(phaseName));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static string BuildBiosErrorMessage(string sceneName, int buildIndex)
        {
            StringBuilder builder = _biosErrorMessageBuilder;
            builder.Length = 0;
            builder.Append("BIOS ERROR 0xBOOT")
                .Append('\n').Append("EXPECTED: 00_BOOTSTRAP [0]")
                .Append('\n').Append("DETECTED: ").Append(sceneName).Append(" [").Append(buildIndex).Append(']')
                .Append('\n').Append("ACTION: FORCED RECOVERY");
            return builder.ToString();
        }

        private static string BuildFatalBootOverlayMessage(string phaseName)
        {
            StringBuilder builder = _fatalOverlayMessageBuilder;
            builder.Length = 0;
            builder.Append("BIOS ERROR 0xBOOT_FATAL")
                .Append('\n').Append("PHASE: ").Append(string.IsNullOrEmpty(phaseName) ? "Unknown" : phaseName)
                .Append('\n').Append("ACTION: SEE fatal_boot_crash.log");
            return builder.ToString();
        }

        private static string BuildFatalBootstrapMessage(string phaseName, Exception exception)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            StringBuilder builder = _fatalCrashMessageBuilder;
            builder.Length = 0;
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

            string truncatedMessage = message;
            int requiredBytes = _fatalBootCrashEncoding.GetByteCount(truncatedMessage);
            while (requiredBytes > FatalBootCrashLogBufferBytes && truncatedMessage.Length > 1)
            {
                truncatedMessage = truncatedMessage.Substring(0, truncatedMessage.Length >> 1);
                requiredBytes = _fatalBootCrashEncoding.GetByteCount(truncatedMessage);
            }

            if (requiredBytes <= 0)
                return;

            string absolutePath = HectonPersistentPathPolicy.CombineFile(FatalBootCrashFileName);
            HectonPersistentPathPolicy.EnsureParentDirectory(absolutePath);
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

        private static bool IsMainMenuScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.isLoaded &&
                   string.Equals(scene.name, MainMenuSceneName, System.StringComparison.Ordinal);
        }

        private static bool RequiresGameplaySceneActivation(Scene scene)
        {
            return scene.IsValid() &&
                   scene.isLoaded &&
                   !IsBootstrapScene(scene) &&
                   !IsMainMenuScene(scene);
        }

    }

    /// <summary>
    /// Silent audio fallback used when an optional audio bootstrap owner cannot initialize.
    /// </summary>
    internal sealed class NoOpAudioService : IAudioService
    {
        // COLD ALLOC: NoOpAudioService[1] - non-critical audio fallback for deterministic bootstrap progress - owner: GameBootstrapper
        internal static readonly NoOpAudioService Shared = new NoOpAudioService();

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
        public bool QueueAudioEvent(in CoreAudioEvent audioEvent)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueuePrologueAudioTransition(in AudioTransitionState state)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueueSoundEmissionSignal(in SoundEmissionSignal signal)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueueHullStressSignal(in HullStressSignal signal)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)
        {
            return false;
        }

        /// <inheritdoc />
        public void PlayStatic2D(AudioClip clip, float volume = 1f)
        {
        }

        /// <inheritdoc />
        public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
        {
        }

        /// <summary>
        /// Compatibility stub for legacy callers that still name one-shot playback.
        /// </summary>
        public void PlayOneShot(AudioClip clip)
        {
        }

        /// <summary>
        /// Compatibility stub for legacy callers that still name one-shot playback.
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volume)
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
        public bool TryEmitModAcousticPing(UnityEngine.Vector3 runtimePosition, float intensity01)
        {
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
    internal sealed class DemiurgeFaunaSimulationService : IFaunaSim, IServiceHeartbeat, IServiceShutdown
    {
        // COLD ALLOC: DemiurgeFaunaSimulationService[1] - headless data-only fauna simulation sentinel - owner: GameBootstrapper
        internal static readonly DemiurgeFaunaSimulationService Shared = new DemiurgeFaunaSimulationService();

        /// <inheritdoc />
        public bool IsReady => true;

        /// <inheritdoc />
        public int ResidentSlotCapacity => 0;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => ServiceHeartbeatState.Ready;

        /// <inheritdoc />
        public bool IsServiceReady => true;

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
        }
    }

    internal static class BootstrapBiosErrorOverlay
    {
        internal static bool Show(string message)
        {
            return HardwareErrorCanvas.Show(message);
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

        private static HardwareErrorCanvas _runtimeOverlay;

        private Text _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeOverlay = null;
        }

        internal static bool Show(string message)
        {
            if (GameBootstrapper.IsHeadlessBootMode || Application.isBatchMode)
            {
                // One-time critical init failure; headless cannot render the BIOS canvas.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(message);
#endif
                return false;
            }

            try
            {
                HardwareErrorCanvas overlay = EnsureInstance();
                if (overlay == null)
                    return false;

                return overlay.ApplyMessage(message);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
                return false;
            }
        }

        internal static void Hide()
        {
            if (_runtimeOverlay == null)
                return;

            GameObject root = _runtimeOverlay.gameObject;
            _runtimeOverlay = null;

            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        private static HardwareErrorCanvas EnsureInstance()
        {
            if (_runtimeOverlay != null)
                return _runtimeOverlay;

            GameObject runtimeRoot = new GameObject(OverlayRootName); // COLD ALLOC: GameObject[1] - hardware-error BIOS fallback overlay root - owner: HardwareErrorCanvas
            GameBootstrapper.EnsureRuntimeInstance();
            HardwareErrorCanvas overlay = runtimeRoot.AddComponent<HardwareErrorCanvas>();
            GameBootstrapper.PersistRuntimeService(overlay);
            return overlay;
        }

        private void Awake()
        {
            if (_runtimeOverlay != null && _runtimeOverlay != this)
            {
                Destroy(gameObject);
                return;
            }

            _runtimeOverlay = this;

            if (Application.isPlaying)
                GameBootstrapper.PersistRuntimeService(this);

            BuildVisualTree();
        }

        private bool ApplyMessage(string message)
        {
            if (_messageText == null)
                BuildVisualTree();

            if (_messageText == null)
                return false;

            _messageText.text = message;
            return true;
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
