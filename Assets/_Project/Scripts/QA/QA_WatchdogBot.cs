#if UNITY_EDITOR || DEVELOPMENT_BUILD
// ============================================================================
// HECTON-8 - QA_WatchdogBot.cs
// QA-only PlayMode execution auditor for menu-to-world endurance validation.
// Hot path rules: no strings, no LINQ, no scene searches, no Debug.Log, no
// managed collections growth, no file I/O. Terminal exports are cold-path only.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hecton.UI.MainMenu;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Hecton8.QA
{
    public enum QAWatchdogState1424 : ushort
    {
        ColdStart = 0,
        WaitBootstrap = 1,
        WaitMainMenu = 2,
        InvokeMenuStart = 3,
        WaitWorld = 4,
        Simulation = 5,
        Completed = 6,
        Failed = 7
    }

    public enum QAWatchdogFailReason1424 : uint
    {
        None = 0u,
        FrameSpike = 1u,
        GcAlloc = 2u,
        VramTargetExceeded = 3u,
        NoKccVelocity = 4u,
        MetricStorageUnavailable = 5u,
        SceneRouteTimeout = 6u,
        NativeSentinelLeak = 7u,
        NonFinitePlayerState = 8u
    }

    [Flags]
    public enum QAWatchdogMetricFlags1424 : ushort
    {
        None = 0,
        DataVaultWriteFailed = 1 << 0,
        ManagedFallback = 1 << 1,
        KccVelocityFresh = 1 << 2,
        PlayerStateFresh = 1 << 3,
        FrameSpike = 1 << 4,
        GcAllocated = 1 << 5,
        VramExceeded = 1 << 6,
        StuckRecovery = 1 << 7,
        SceneFallbackUsed = 1 << 8,
        NativeSentinelFailed = 1 << 9,
        MockFuzzerArmed = 1 << 10
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct QAWatchdogFrameMetric1424
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public ushort FrameTimeMicroseconds;
        [FieldOffset(6)] public ushort Batches;
        [FieldOffset(8)] public uint GcAllocBytes;
        [FieldOffset(12)] public ushort VramMegabytes;
        [FieldOffset(14)] public ushort SetPassCalls;
        [FieldOffset(16)] public float AupX;
        [FieldOffset(20)] public float AupY;
        [FieldOffset(24)] public float AupZ;
        [FieldOffset(28)] public ushort Flags;
        [FieldOffset(30)] public ushort State;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct QAWatchdogBlackBoxEntry1424
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public ushort FrameTimeMicroseconds;
        [FieldOffset(6)] public ushort Batches;
        [FieldOffset(8)] public uint GcAllocBytes;
        [FieldOffset(12)] public ushort VramMegabytes;
        [FieldOffset(14)] public ushort SetPassCalls;
        [FieldOffset(16)] public float AupX;
        [FieldOffset(20)] public float AupY;
        [FieldOffset(24)] public float AupZ;
        [FieldOffset(28)] public ushort Flags;
        [FieldOffset(30)] public ushort State;
        [FieldOffset(32)] public uint FailReason;
        [FieldOffset(36)] public uint ConsecutiveSpikeFrames;
        [FieldOffset(40)] public float DistanceMeters;
        [FieldOffset(44)] public float RollingP95Milliseconds;
        [FieldOffset(48)] public uint MenuResolveAttempts;
        [FieldOffset(52)] public uint SceneFallbackAttempts;
        [FieldOffset(56)] public uint VaultWriteFailures;
        [FieldOffset(60)] public uint DefragRequests;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/QA/QA Watchdog Bot 1424")]
    public sealed class QA_WatchdogBot :
        MonoBehaviour,
        IFastTickable,
        IColdTickable,
        ILateFrameTickable,
        IGlobalRegistryHotSwapListener
    {
        private const string AutoObjectName = "__QA_WATCHDOG_BOT_1424";
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const int MainMenuSceneBuildIndex = 1;
        private const int OrbitSceneBuildIndex = 2;
        private const int WorldSceneBuildIndex = 3;
        private const string CommandLineSwitch = "-h8QaWatchdog1424";
        private const string EnvironmentSwitch = "H8_QA_WATCHDOG_1424";
        private const string FlagFile = "Temp/H8_QA_WATCHDOG_1424.flag";
        private const string CsvFileName = "QA_WATCHDOG_ENDURANCE_REPORT_1424.csv";

        private const int RecorderCapacity = 1;
        private const int MetricCapacity = 36000;
        private const int BlackBoxCapacity = 300;
        private const int P95WindowCapacity = 2048;
        private const int P95BucketCount = 128;
        private const float P95FrameBudgetMilliseconds = 16.67f;
        private const int FrameSpikeMicroseconds = 25000;
        private const int HardFrameSpikeStreak = 3;
        private const int TargetVramMegabytes = 1600;
        private const int HardVramMegabytes = 1800;
        private const float TargetDistanceMeters = 10000f;
        private const float StuckProbeSeconds = 4f;
        private const float NoKccVelocityFailSeconds = 30f;
        private const float SceneRouteTimeoutSeconds = 40f;
        private const float DefragCadenceSeconds = 60f;
        private const float BytesPerMegabyte = 1024f * 1024f;

        private const BufferID MetricsBufferId = (BufferID)74240;
        private const BufferID BlackBoxBufferId = (BufferID)74241;
        private const SystemID OwnerSystemId = SystemID.QAEndurance;

        private static readonly string[] FrameTimeCounters = { "CPU Total Frame Time", "Frame Time" };
        private static readonly string[] GcAllocCounters = { "GC Allocated In Frame" };
        private static readonly string[] GfxUsedCounters = { "Gfx Used Memory", "Gfx Used Memory Bytes" };
        private static readonly string[] BatchesCounters = { "Batches Count" };
        private static readonly string[] SetPassCounters = { "SetPass Calls Count" };
        private static readonly Type[] SentinelShutdownAssertSignature = { typeof(string) }; // COLD ALLOC: Type[1] - terminal sentinel reflection signature - owner: QA_WatchdogBot

        [SerializeField] private bool runOnEnable = true;
        [SerializeField] private bool failOnGcAlloc = true;
        [SerializeField] private bool enableMockGcFuzzer;
        [SerializeField, Range(0f, 1f)] private float fallbackQualityWeight = 0f;

        private readonly List<GameObject> _sceneRootScratch = new List<GameObject>(512); // COLD ALLOC: List<GameObject>[512] - main-menu root traversal scratch - owner: QA_WatchdogBot
        private readonly object[] _sentinelAssertArgs = new object[1]; // COLD ALLOC: object[1] - terminal sentinel reflection args - owner: QA_WatchdogBot
        private readonly int[] _p95Buckets = new int[P95BucketCount]; // COLD ALLOC: int[P95BucketCount] - p95 histogram buckets - owner: QA_WatchdogBot
        private readonly byte[] _p95BucketRing = new byte[P95WindowCapacity]; // COLD ALLOC: byte[P95WindowCapacity] - p95 rolling bucket ring - owner: QA_WatchdogBot
        private readonly char[] _formatBuffer = new char[64]; // COLD ALLOC: char[64] - terminal CSV TryFormat scratch - owner: QA_WatchdogBot

        private QAWatchdogFrameMetric1424[] _managedMetrics;
        private QAWatchdogBlackBoxEntry1424[] _managedBlackBox;
        private VaultGenerationHandle<QAWatchdogFrameMetric1424> _metricHandle;
        private VaultGenerationHandle<QAWatchdogBlackBoxEntry1424> _blackBoxHandle;
        private IDataVault _dataVault;
        private ISceneService _sceneService;

        private ProfilerRecorder _frameTimeRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _gfxUsedRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _setPassRecorder;

        private QAWatchdogState1424 _state;
        private QAWatchdogFailReason1424 _failReason;
        private bool _runActive;
        private bool _terminalExportQueued;
        private bool _terminalExportWritten;
        private bool _tickRegistered;
        private bool _coldRegistered;
        private bool _lateRegistered;
        private bool _hotSwapRegistered;
        private bool _quitHookRegistered;
        private bool _storageReady;
        private bool _usingManagedFallback;
        private bool _menuStartInvoked;
        private bool _menuStartRequestPending;
        private bool _worldSceneRequestPending;
        private bool _vaultDefragRequestPending;
        private bool _sceneFallbackUsed;
        private bool _nativeSentinelFailed;
        private MainMenuController _mainMenuController;
        private int _metricWriteIndex;
        private int _metricSamples;
        private int _blackBoxWriteIndex;
        private int _blackBoxSamples;
        private int _p95WriteIndex;
        private int _p95SampleCount;
        private uint _consecutiveSpikeFrames;
        private uint _menuResolveAttempts;
        private uint _sceneFallbackAttempts;
        private uint _vaultWriteFailures;
        private uint _defragRequests;
        private float _rollingP95Milliseconds;
        private float _distanceMeters;
        private float _simulationSeconds;
        private float _sceneWaitSeconds;
        private float _noKccVelocitySeconds;
        private float _stuckSeconds;
        private float _defragSeconds;
        private float _resolvedQualityWeight01;
        private float _avoidancePhase;
        private float _lastFastDeltaTimeSeconds;
        private float3 _lastWorldPosition;
        private float3 _currentWorldPosition;
        private bool _lastKccVelocityFresh;
        private bool _lastPlayerStateFresh;
        private bool _hasLastWorldPosition;
        private string _csvPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateFromBatchFlag()
        {
            if (!ShouldAutoStartCold())
                return;

            GameObject existing = GameObject.Find(AutoObjectName);
            if (existing != null)
                return;

            GameObject botObject = new GameObject(AutoObjectName);
            DontDestroyOnLoad(botObject);
            botObject.AddComponent<QA_WatchdogBot>();
        }

        private static bool ShouldAutoStartCold()
        {
            if (string.Equals(Environment.GetEnvironmentVariable(EnvironmentSwitch), "1", StringComparison.Ordinal))
                return true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], CommandLineSwitch, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return File.Exists(ResolveProjectPathStatic(FlagFile));
        }

        private void Awake()
        {
            ResolveArtifactPathsCold();
            _sentinelAssertArgs[0] = "QA_WATCHDOG_BOT_1424";
        }

        private void OnEnable()
        {
            RegisterQuitHookCold();
            if (runOnEnable)
                BeginRunCold();
        }

        private void OnDisable()
        {
            UnregisterQuitHookCold();
            StopRunCold();
        }

        private void OnDestroy()
        {
            StopRunCold();
        }

        private void BeginRunCold()
        {
            if (_runActive)
                return;

            _runActive = true;
            _state = QAWatchdogState1424.ColdStart;
            _failReason = QAWatchdogFailReason1424.None;
            _terminalExportQueued = false;
            _terminalExportWritten = false;
            _metricWriteIndex = 0;
            _metricSamples = 0;
            _blackBoxWriteIndex = 0;
            _blackBoxSamples = 0;
            _p95WriteIndex = 0;
            _p95SampleCount = 0;
            _consecutiveSpikeFrames = 0u;
            _menuResolveAttempts = 0u;
            _sceneFallbackAttempts = 0u;
            _vaultWriteFailures = 0u;
            _defragRequests = 0u;
            _rollingP95Milliseconds = 0f;
            _distanceMeters = 0f;
            _simulationSeconds = 0f;
            _sceneWaitSeconds = 0f;
            _noKccVelocitySeconds = 0f;
            _stuckSeconds = 0f;
            _defragSeconds = 0f;
            _resolvedQualityWeight01 = ResolveGlobalQualityWeight01();
            _avoidancePhase = 0f;
            _lastFastDeltaTimeSeconds = 0f;
            _lastKccVelocityFresh = false;
            _lastPlayerStateFresh = false;
            _hasLastWorldPosition = false;
            _menuStartInvoked = false;
            _menuStartRequestPending = false;
            _worldSceneRequestPending = false;
            _vaultDefragRequestPending = false;
            _sceneFallbackUsed = false;
            _nativeSentinelFailed = false;
            _mainMenuController = null;

            Array.Clear(_p95Buckets, 0, _p95Buckets.Length);
            Array.Clear(_p95BucketRing, 0, _p95BucketRing.Length);

            _managedMetrics = null;
            _managedBlackBox = null;

            ResolveRecordersCold();
            CacheDataVaultCold();
            CacheSceneServiceCold();
            EnsureStorageCold();
            TryRegisterHotSwapListenerCold();
            RegisterTickLanesCold();
            _state = QAWatchdogState1424.WaitBootstrap;
        }

        private void ResolveArtifactPathsCold()
        {
            string reportRoot = ResolveProjectPathStatic("Docs/Reports");
            _csvPath = Path.Combine(reportRoot, CsvFileName);
        }

        private static string ResolveProjectPathStatic(string relativePath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(projectRoot, "Assets")) && !string.IsNullOrEmpty(Application.dataPath))
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                if (parent != null)
                    projectRoot = parent.FullName;
            }

            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private void ResolveRecordersCold()
        {
            _frameTimeRecorder = StartRecorderCold(ProfilerCategory.Internal, FrameTimeCounters);
            _gcAllocRecorder = StartRecorderCold(ProfilerCategory.Memory, GcAllocCounters);
            _gfxUsedRecorder = StartRecorderCold(ProfilerCategory.Memory, GfxUsedCounters);
            _batchesRecorder = StartRecorderCold(ProfilerCategory.Render, BatchesCounters);
            _setPassRecorder = StartRecorderCold(ProfilerCategory.Render, SetPassCounters);
        }

        private static ProfilerRecorder StartRecorderCold(ProfilerCategory category, string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                        category,
                        candidates[i],
                        RecorderCapacity,
                        ProfilerRecorderOptions.Default);
                    if (recorder.Valid)
                        return recorder;
                }
                catch (ArgumentException)
                {
                }
            }

            return default;
        }

        private void CacheDataVaultCold()
        {
            _dataVault = GlobalRegistry.DataVault;
        }

        private void CacheSceneServiceCold()
        {
            _sceneService = GlobalRegistry.Scene;
        }

        private void EnsureStorageCold()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                _metricHandle = vault.EnsureGenerationHandle<QAWatchdogFrameMetric1424>(
                    MetricsBufferId,
                    MetricCapacity,
                    OwnerSystemId,
                    NativeArrayOptions.ClearMemory);
                _blackBoxHandle = vault.EnsureGenerationHandle<QAWatchdogBlackBoxEntry1424>(
                    BlackBoxBufferId,
                    BlackBoxCapacity,
                    OwnerSystemId,
                    NativeArrayOptions.ClearMemory);

                _storageReady = IsHandleCreated(in _metricHandle) && IsHandleCreated(in _blackBoxHandle);
                if (_storageReady)
                {
                    _usingManagedFallback = false;
                    return;
                }
            }

            EnsureManagedFallbackCold();
            _storageReady = true;
            _usingManagedFallback = true;
        }

        private void EnsureManagedFallbackCold()
        {
            if (_managedMetrics == null || _managedMetrics.Length != MetricCapacity)
                _managedMetrics = new QAWatchdogFrameMetric1424[MetricCapacity]; // COLD ALLOC: managed fallback metric ring - owner: QA_WatchdogBot
            if (_managedBlackBox == null || _managedBlackBox.Length != BlackBoxCapacity)
                _managedBlackBox = new QAWatchdogBlackBoxEntry1424[BlackBoxCapacity]; // COLD ALLOC: managed fallback black-box ring - owner: QA_WatchdogBot
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        public void FastTick(float deltaTime)
        {
            if (!_runActive || _terminalExportQueued)
                return;

            if (deltaTime > 0f && math.isfinite(deltaTime))
                _lastFastDeltaTimeSeconds = deltaTime;

            switch (_state)
            {
                case QAWatchdogState1424.WaitBootstrap:
                    FastTickWaitBootstrap(deltaTime);
                    break;
                case QAWatchdogState1424.WaitMainMenu:
                    FastTickWaitMainMenu(deltaTime);
                    break;
                case QAWatchdogState1424.InvokeMenuStart:
                    FastTickInvokeMenuStart(deltaTime);
                    break;
                case QAWatchdogState1424.WaitWorld:
                    FastTickWaitWorld(deltaTime);
                    break;
                case QAWatchdogState1424.Simulation:
                    FastTickSimulation(deltaTime);
                    break;
            }
        }

        private void FastTickWaitBootstrap(float deltaTime)
        {
            _sceneWaitSeconds += deltaTime;
            int activeSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
            if (activeSceneBuildIndex == MainMenuSceneBuildIndex)
            {
                _sceneWaitSeconds = 0f;
                _state = QAWatchdogState1424.WaitMainMenu;
                return;
            }

            if (activeSceneBuildIndex == WorldSceneBuildIndex)
            {
                EnterSimulationState();
                return;
            }

            if (_sceneWaitSeconds > SceneRouteTimeoutSeconds)
                FailCold(QAWatchdogFailReason1424.SceneRouteTimeout);
        }

        private void FastTickWaitMainMenu(float deltaTime)
        {
            _sceneWaitSeconds += deltaTime;
            if (_menuStartInvoked)
            {
                _sceneWaitSeconds = 0f;
                _state = QAWatchdogState1424.WaitWorld;
                return;
            }

            _menuStartRequestPending = true;
            if (_sceneWaitSeconds > SceneRouteTimeoutSeconds)
                FailCold(QAWatchdogFailReason1424.SceneRouteTimeout);
        }

        private void FastTickInvokeMenuStart(float deltaTime)
        {
            FastTickWaitMainMenu(deltaTime);
        }

        private void FastTickWaitWorld(float deltaTime)
        {
            _sceneWaitSeconds += deltaTime;
            int activeSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
            if (activeSceneBuildIndex == WorldSceneBuildIndex)
            {
                EnterSimulationState();
                return;
            }

            if (!_sceneFallbackUsed &&
                (_sceneWaitSeconds > 8f || activeSceneBuildIndex == OrbitSceneBuildIndex))
            {
                _worldSceneRequestPending = true;
                return;
            }

            if (_sceneWaitSeconds > SceneRouteTimeoutSeconds)
                FailCold(QAWatchdogFailReason1424.SceneRouteTimeout);
        }

        private void EnterSimulationState()
        {
            _state = QAWatchdogState1424.Simulation;
            _simulationSeconds = 0f;
            _sceneWaitSeconds = 0f;
            _noKccVelocitySeconds = 0f;
            _stuckSeconds = 0f;
            _defragSeconds = 0f;
            _hasLastWorldPosition = false;
            _lastKccVelocityFresh = false;
            _lastPlayerStateFresh = false;
            CoreDeterminismSignals.ClearInputOverride();
        }

        private bool TryInvokeMainMenuStartCold()
        {
            if (_menuStartInvoked)
                return true;

            _menuResolveAttempts++;
            MainMenuController controller = _mainMenuController;
            if (controller == null)
            {
                controller = ResolveMainMenuControllerCold();
                _mainMenuController = controller;
            }

            if (controller == null)
                return false;

            controller.StartGame(string.Empty);
            _menuStartInvoked = true;
            return true;
        }

        private MainMenuController ResolveMainMenuControllerCold()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            _sceneRootScratch.Clear();
            scene.GetRootGameObjects(_sceneRootScratch);
            for (int i = 0; i < _sceneRootScratch.Count; i++)
            {
                GameObject root = _sceneRootScratch[i];
                if (root == null)
                    continue;

                if (root.TryGetComponent(out MainMenuController controller))
                {
                    _sceneRootScratch.Clear();
                    return controller;
                }

                controller = FindMainMenuControllerInChildrenCold(root.transform);
                if (controller != null)
                {
                    _sceneRootScratch.Clear();
                    return controller;
                }
            }

            _sceneRootScratch.Clear();
            return null;
        }

        private static MainMenuController FindMainMenuControllerInChildrenCold(Transform root)
        {
            if (root == null)
                return null;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                if (child.TryGetComponent(out MainMenuController controller))
                    return controller;

                controller = FindMainMenuControllerInChildrenCold(child);
                if (controller != null)
                    return controller;
            }

            return null;
        }

        private bool TryRequestWorldSceneCold()
        {
            _sceneFallbackAttempts++;
            ISceneService sceneService = _sceneService;
            if (sceneService != null)
            {
                sceneService.LoadScene(WorldSceneName);
                return true;
            }

            SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Single);
            return true;
        }

        private void FastTickSimulation(float deltaTime)
        {
            _simulationSeconds += deltaTime;
            _defragSeconds += deltaTime;
            PublishDriveInputHot();
            IntegrateDistanceHot(deltaTime);
            if (_terminalExportQueued)
                return;

            if (_defragSeconds >= DefragCadenceSeconds)
            {
                _defragSeconds = 0f;
                _vaultDefragRequestPending = true;
            }

            if (_distanceMeters >= TargetDistanceMeters)
                CompleteCold();
        }

        private void PublishDriveInputHot()
        {
            PlayerInputState state = default;
            float quality = _resolvedQualityWeight01;
            float cruiseLateralAmplitude = math.lerp(0.14f, 0.28f, quality);
            float recoveryLateralAmplitude = math.lerp(0.55f, 0.95f, quality);
            float cruiseVertical = math.lerp(0.05f, 0.1f, quality);
            float recoveryVertical = math.lerp(0.2f, 0.34f, quality);
            float lateral = ResolveTriangleWave(_simulationSeconds * 0.125f) * cruiseLateralAmplitude;
            if (_stuckSeconds > 1.5f)
            {
                _avoidancePhase += 0.25f;
                lateral = ResolveTriangleWave(_avoidancePhase) * recoveryLateralAmplitude;
            }

            state.MoveDelta.x = lateral;
            state.MoveDelta.y = 1f;
            state.LookDelta.x = lateral * 0.015f;
            state.LookDelta.y = -0.006f;
            state.VerticalDelta = _stuckSeconds > 1.5f ? recoveryVertical : cruiseVertical;
            state.ActionsBitmask = (uint)PlayerInputAction.Sprint;
            CoreDeterminismSignals.TryPublishInputOverride(in state, SystemDispatcher.CurrentFrameId);
        }

        private float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(quality))
                quality = fallbackQualityWeight;

            return math.saturate(quality);
        }

        private static float ResolveTriangleWave(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return wrapped < 0.5f ? (wrapped * 4f) - 1f : 3f - (wrapped * 4f);
        }

        private void IntegrateDistanceHot(float deltaTime)
        {
            bool hasPlayer = TryResolvePlayerStateHot(out PlayerMovementRuntimeState movementState);
            _lastPlayerStateFresh = hasPlayer;
            bool finitePlayer = !hasPlayer || IsFinite(in movementState);
            if (!finitePlayer)
            {
                FailCold(QAWatchdogFailReason1424.NonFinitePlayerState);
                return;
            }

            bool hasVelocity = CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(8u, out float3 velocity);
            _lastKccVelocityFresh = hasVelocity;
            if (!hasVelocity)
            {
                _noKccVelocitySeconds += deltaTime;
                if (_noKccVelocitySeconds >= NoKccVelocityFailSeconds)
                    FailCold(QAWatchdogFailReason1424.NoKccVelocity);
                return;
            }

            _noKccVelocitySeconds = 0f;
            _currentWorldPosition = hasPlayer ? movementState.WorldPosition : _currentWorldPosition + velocity * deltaTime;
            float speed = math.length(velocity);
            if (speed < 0.08f)
                _stuckSeconds += deltaTime;
            else
                _stuckSeconds = 0f;

            if (!_hasLastWorldPosition)
            {
                _lastWorldPosition = _currentWorldPosition;
                _hasLastWorldPosition = true;
                return;
            }

            float deltaMeters = speed * deltaTime;
            if (deltaMeters > 0f && deltaMeters < 25f && math.isfinite(deltaMeters))
                _distanceMeters += deltaMeters;

            _lastWorldPosition = _currentWorldPosition;
        }

        private static bool TryResolvePlayerStateHot(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                !runtimeContext.IsBound)
            {
                return false;
            }

            movementState = runtimeContext.MovementState;
            return true;
        }

        private static bool IsFinite(in PlayerMovementRuntimeState state)
        {
            return math.all(math.isfinite(state.WorldPosition)) &&
                   math.all(math.isfinite(state.PredictedWorldPosition)) &&
                   math.all(math.isfinite(state.Velocity)) &&
                   math.all(math.isfinite(state.Forward)) &&
                   math.all(math.isfinite(state.CameraForward));
        }

        public void LateFrameTick()
        {
            if (!_runActive || _terminalExportWritten)
                return;

            if (_terminalExportQueued)
            {
                WriteTerminalArtifactsCold();
                return;
            }

            QAWatchdogFrameMetric1424 metric = SampleMetricHot();
            WriteMetricHot(in metric);
            WriteBlackBoxHot(in metric);
            EvaluateMetricHot(in metric);
        }

        private QAWatchdogFrameMetric1424 SampleMetricHot()
        {
            long graphicsFallbackBytes = _gfxUsedRecorder.Valid ? 0L : Profiler.GetAllocatedMemoryForGraphicsDriver();
            uint frameUs = ClampUInt(ReadRecorderMicroseconds(_frameTimeRecorder, _lastFastDeltaTimeSeconds));
            uint gcBytes = ClampUInt(ReadRecorderBytes(_gcAllocRecorder, 0L));
            uint vramMb = ClampUInt(ReadRecorderMegabytes(_gfxUsedRecorder, graphicsFallbackBytes));
            uint batches = ClampUInt(ReadRecorderRaw(_batchesRecorder, 0L));
            uint setPass = ClampUInt(ReadRecorderRaw(_setPassRecorder, 0L));

            QAWatchdogMetricFlags1424 flags = QAWatchdogMetricFlags1424.None;
            if (_usingManagedFallback)
                flags |= QAWatchdogMetricFlags1424.ManagedFallback;
            if (_sceneFallbackUsed)
                flags |= QAWatchdogMetricFlags1424.SceneFallbackUsed;
            if (_nativeSentinelFailed)
                flags |= QAWatchdogMetricFlags1424.NativeSentinelFailed;
            if (enableMockGcFuzzer)
                flags |= QAWatchdogMetricFlags1424.MockFuzzerArmed;
            if (_stuckSeconds > 1.5f)
                flags |= QAWatchdogMetricFlags1424.StuckRecovery;
            if (_lastKccVelocityFresh)
                flags |= QAWatchdogMetricFlags1424.KccVelocityFresh;
            if (_lastPlayerStateFresh)
                flags |= QAWatchdogMetricFlags1424.PlayerStateFresh;
            if (frameUs > FrameSpikeMicroseconds)
                flags |= QAWatchdogMetricFlags1424.FrameSpike;
            if (gcBytes > 0u)
                flags |= QAWatchdogMetricFlags1424.GcAllocated;
            if (vramMb >= TargetVramMegabytes)
                flags |= QAWatchdogMetricFlags1424.VramExceeded;

            byte p95Bucket = ResolveP95Bucket(frameUs);
            UpdateP95Hot(p95Bucket);

            QAWatchdogFrameMetric1424 metric = default;
            metric.Frame = SystemDispatcher.CurrentFrameId;
            metric.FrameTimeMicroseconds = ClampUShort(frameUs);
            metric.Batches = ClampUShort(batches);
            metric.GcAllocBytes = gcBytes;
            metric.VramMegabytes = ClampUShort(vramMb);
            metric.SetPassCalls = ClampUShort(setPass);
            metric.AupX = _currentWorldPosition.x;
            metric.AupY = _currentWorldPosition.y;
            metric.AupZ = _currentWorldPosition.z;
            metric.Flags = (ushort)flags;
            metric.State = (ushort)_state;
            return metric;
        }

        private static long ReadRecorderRaw(ProfilerRecorder recorder, long fallback)
        {
            return recorder.Valid ? recorder.LastValue : fallback;
        }

        private static long ReadRecorderMicroseconds(ProfilerRecorder recorder, float fallbackSeconds)
        {
            if (!recorder.Valid)
                return (long)(fallbackSeconds * 1000000f);

            long value = recorder.LastValue;
            if (value <= 0L)
                return 0L;

            return recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds
                ? value / 1000L
                : value * 1000L;
        }

        private static long ReadRecorderBytes(ProfilerRecorder recorder, long fallback)
        {
            if (!recorder.Valid)
                return fallback;

            long value = recorder.LastValue;
            return value > 0L ? value : 0L;
        }

        private static long ReadRecorderMegabytes(ProfilerRecorder recorder, long fallbackBytes)
        {
            if (!recorder.Valid)
                return fallbackBytes > 0L ? fallbackBytes / (1024L * 1024L) : 0L;

            long value = recorder.LastValue;
            if (value <= 0L)
                return 0L;

            return recorder.UnitType == ProfilerMarkerDataUnit.Bytes
                ? value / (1024L * 1024L)
                : value;
        }

        private static uint ClampUInt(long value)
        {
            if (value <= 0L)
                return 0u;
            return value > uint.MaxValue ? uint.MaxValue : (uint)value;
        }

        private static ushort ClampUShort(uint value)
        {
            return value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;
        }

        private static byte ResolveP95Bucket(uint frameUs)
        {
            uint bucket = frameUs / 1000u;
            return bucket >= P95BucketCount ? (byte)(P95BucketCount - 1) : (byte)bucket;
        }

        private void UpdateP95Hot(byte bucket)
        {
            if (_p95SampleCount < P95WindowCapacity)
            {
                _p95SampleCount++;
            }
            else
            {
                byte oldBucket = _p95BucketRing[_p95WriteIndex];
                _p95Buckets[oldBucket]--;
            }

            _p95BucketRing[_p95WriteIndex] = bucket;
            _p95Buckets[bucket]++;
            _p95WriteIndex++;
            if (_p95WriteIndex >= P95WindowCapacity)
                _p95WriteIndex = 0;
        }

        private void WriteMetricHot(in QAWatchdogFrameMetric1424 metric)
        {
            int slot = _metricWriteIndex;
            _metricWriteIndex++;
            if (_metricWriteIndex >= MetricCapacity)
                _metricWriteIndex = 0;
            _metricSamples++;

            IDataVault vault = _dataVault;
            if (!_usingManagedFallback &&
                vault != null &&
                IsHandleCreated(in _metricHandle))
            {
                bool lockAcquired = false;
                NativeArray<QAWatchdogFrameMetric1424> metrics = default;
                try
                {
                    lockAcquired = vault.TryAcquireWriteLock(in _metricHandle, OwnerSystemId, out metrics);
                    if (lockAcquired && metrics.IsCreated && slot < metrics.Length)
                    {
                        metrics[slot] = metric;
                        return;
                    }
                }
                finally
                {
                    if (lockAcquired)
                        vault.ReleaseWriteLock(in _metricHandle, OwnerSystemId);
                }
            }

            _vaultWriteFailures++;
            if (_managedMetrics != null && slot < _managedMetrics.Length)
                _managedMetrics[slot] = metric;
        }

        private void WriteBlackBoxHot(in QAWatchdogFrameMetric1424 metric)
        {
            QAWatchdogBlackBoxEntry1424 entry = default;
            entry.Frame = metric.Frame;
            entry.FrameTimeMicroseconds = metric.FrameTimeMicroseconds;
            entry.Batches = metric.Batches;
            entry.GcAllocBytes = metric.GcAllocBytes;
            entry.VramMegabytes = metric.VramMegabytes;
            entry.SetPassCalls = metric.SetPassCalls;
            entry.AupX = metric.AupX;
            entry.AupY = metric.AupY;
            entry.AupZ = metric.AupZ;
            entry.Flags = metric.Flags;
            entry.State = metric.State;
            entry.FailReason = (uint)_failReason;
            entry.ConsecutiveSpikeFrames = _consecutiveSpikeFrames;
            entry.DistanceMeters = _distanceMeters;
            entry.RollingP95Milliseconds = _rollingP95Milliseconds;
            entry.MenuResolveAttempts = _menuResolveAttempts;
            entry.SceneFallbackAttempts = _sceneFallbackAttempts;
            entry.VaultWriteFailures = _vaultWriteFailures;
            entry.DefragRequests = _defragRequests;

            int slot = _blackBoxWriteIndex;
            _blackBoxWriteIndex++;
            if (_blackBoxWriteIndex >= BlackBoxCapacity)
                _blackBoxWriteIndex = 0;
            _blackBoxSamples++;

            IDataVault vault = _dataVault;
            if (!_usingManagedFallback &&
                vault != null &&
                IsHandleCreated(in _blackBoxHandle))
            {
                bool lockAcquired = false;
                NativeArray<QAWatchdogBlackBoxEntry1424> entries = default;
                try
                {
                    lockAcquired = vault.TryAcquireWriteLock(in _blackBoxHandle, OwnerSystemId, out entries);
                    if (lockAcquired && entries.IsCreated && slot < entries.Length)
                    {
                        entries[slot] = entry;
                        return;
                    }
                }
                finally
                {
                    if (lockAcquired)
                        vault.ReleaseWriteLock(in _blackBoxHandle, OwnerSystemId);
                }
            }

            _vaultWriteFailures++;
            if (_managedBlackBox != null && slot < _managedBlackBox.Length)
                _managedBlackBox[slot] = entry;
        }

        private void EvaluateMetricHot(in QAWatchdogFrameMetric1424 metric)
        {
            if (metric.FrameTimeMicroseconds > FrameSpikeMicroseconds)
                _consecutiveSpikeFrames++;
            else
                _consecutiveSpikeFrames = 0u;

            if (_consecutiveSpikeFrames >= HardFrameSpikeStreak)
            {
                FailCold(QAWatchdogFailReason1424.FrameSpike);
                return;
            }

            if (failOnGcAlloc && _state == QAWatchdogState1424.Simulation && metric.GcAllocBytes > 0u)
            {
                FailCold(QAWatchdogFailReason1424.GcAlloc);
                return;
            }

            if (metric.VramMegabytes >= TargetVramMegabytes)
            {
                FailCold(QAWatchdogFailReason1424.VramTargetExceeded);
                return;
            }

            if (!_storageReady)
                FailCold(QAWatchdogFailReason1424.MetricStorageUnavailable);
        }

        public void ColdTick()
        {
            if (!_runActive)
                return;

            _resolvedQualityWeight01 = ResolveGlobalQualityWeight01();
            ProcessRouteColdWork();
            ProcessVaultDefragCold();
            _rollingP95Milliseconds = ResolveRollingP95MillisecondsCold();
            if (_state == QAWatchdogState1424.Simulation && _rollingP95Milliseconds > P95FrameBudgetMilliseconds)
                FailCold(QAWatchdogFailReason1424.FrameSpike);
        }

        private void ProcessRouteColdWork()
        {
            if (_state == QAWatchdogState1424.WaitMainMenu ||
                _state == QAWatchdogState1424.InvokeMenuStart)
            {
                if (_menuStartRequestPending && TryInvokeMainMenuStartCold())
                {
                    _menuStartRequestPending = false;
                    _sceneWaitSeconds = 0f;
                    _state = QAWatchdogState1424.WaitWorld;
                }
                return;
            }

            if (_state != QAWatchdogState1424.WaitWorld || !_worldSceneRequestPending)
                return;

            _sceneFallbackUsed = TryRequestWorldSceneCold();
            if (_sceneFallbackUsed)
            {
                _worldSceneRequestPending = false;
                _sceneWaitSeconds = 0f;
            }
        }

        private float ResolveRollingP95MillisecondsCold()
        {
            if (_p95SampleCount <= 0)
                return 0f;

            int target = (int)math.ceil(_p95SampleCount * 0.95f);
            int accumulated = 0;
            for (int bucket = 0; bucket < P95BucketCount; bucket++)
            {
                accumulated += _p95Buckets[bucket];
                if (accumulated >= target)
                    return bucket;
            }

            return P95BucketCount - 1;
        }

        private void ProcessVaultDefragCold()
        {
            if (!_vaultDefragRequestPending)
                return;

            _vaultDefragRequestPending = false;
            RequestVaultDefragCold();
        }

        private void RequestVaultDefragCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _defragRequests++;
            vault.RequestEditorForceDefragmentation();
            vault.FrostTickDefrag(0.2f, 1f, MemoryDefragPhase.PreSimulation, vault.ActiveBurstLockMask);
        }

        private void CompleteCold()
        {
            if (_terminalExportQueued)
                return;

            _state = QAWatchdogState1424.Completed;
            _failReason = QAWatchdogFailReason1424.None;
            _terminalExportQueued = true;
            CoreDeterminismSignals.ClearInputOverride();
        }

        private void FailCold(QAWatchdogFailReason1424 reason)
        {
            if (_terminalExportQueued)
                return;

            _state = QAWatchdogState1424.Failed;
            _failReason = reason;
            _terminalExportQueued = true;
            CoreDeterminismSignals.ClearInputOverride();
        }

        private void WriteTerminalArtifactsCold()
        {
            if (_terminalExportWritten)
                return;

            _terminalExportWritten = true;
            _nativeSentinelFailed = !AssertNativeSentinelCold();
            WriteCsvCold();
            StopRunCold();
        }

        private bool AssertNativeSentinelCold()
        {
            Type sentinelType = Type.GetType("Hecton8.Core.NativeMemorySentinel, Hecton8.Core");
            if (sentinelType == null)
                return true;

            MethodInfo validate = sentinelType.GetMethod("ValidateZeroLeaks", BindingFlags.Static | BindingFlags.Public);
            if (validate != null && validate.ReturnType == typeof(bool))
                return (bool)validate.Invoke(null, null);

            MethodInfo assert = sentinelType.GetMethod(
                "AssertNoAllocationsAfterServiceShutdown",
                BindingFlags.Static | BindingFlags.Public,
                null,
                SentinelShutdownAssertSignature,
                null);
            if (assert == null || assert.ReturnType != typeof(bool))
                return true;

            try
            {
                return (bool)assert.Invoke(null, _sentinelAssertArgs);
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }

        private void WriteCsvCold()
        {
            EnsureDirectoryCold(_csvPath);
            using (StreamWriter writer = new StreamWriter(_csvPath, false, Encoding.UTF8))
            {
                writer.WriteLine("frame,state,frame_time_ms,gc_alloc_bytes,vram_mb,batches,setpass,distance_m,aup_x,aup_y,aup_z,flags");
                int total = math.min(_metricSamples, MetricCapacity);
                int start = _metricSamples < MetricCapacity ? 0 : _metricWriteIndex;

                if (!_usingManagedFallback &&
                    _dataVault != null &&
                    IsHandleCreated(in _metricHandle) &&
                    _dataVault.TryReadOnlyHandle(in _metricHandle, out NativeArray<QAWatchdogFrameMetric1424>.ReadOnly metrics) &&
                    metrics.IsCreated)
                {
                    for (int i = 0; i < total; i++)
                    {
                        int slot = (start + i) % MetricCapacity;
                        if (slot < metrics.Length)
                        {
                            QAWatchdogFrameMetric1424 metric = metrics[slot];
                            WriteCsvRowCold(writer, in metric);
                        }
                    }
                    return;
                }

                if (_managedMetrics == null)
                    return;

                for (int i = 0; i < total; i++)
                {
                    int slot = (start + i) % MetricCapacity;
                    WriteCsvRowCold(writer, in _managedMetrics[slot]);
                }
            }
        }

        private void WriteCsvRowCold(StreamWriter writer, in QAWatchdogFrameMetric1424 metric)
        {
            WriteUIntCold(writer, metric.Frame);
            writer.Write(',');
            WriteStateNameCold(writer, metric.State);
            writer.Write(',');
            WriteFloatCold(writer, metric.FrameTimeMicroseconds * 0.001f, "0.000");
            writer.Write(',');
            WriteUIntCold(writer, metric.GcAllocBytes);
            writer.Write(',');
            WriteUShortCold(writer, metric.VramMegabytes);
            writer.Write(',');
            WriteUShortCold(writer, metric.Batches);
            writer.Write(',');
            WriteUShortCold(writer, metric.SetPassCalls);
            writer.Write(',');
            WriteFloatCold(writer, _distanceMeters, "0.000");
            writer.Write(',');
            WriteFloatCold(writer, metric.AupX, "0.000");
            writer.Write(',');
            WriteFloatCold(writer, metric.AupY, "0.000");
            writer.Write(',');
            WriteFloatCold(writer, metric.AupZ, "0.000");
            writer.Write(',');
            WriteUShortCold(writer, metric.Flags);
            writer.WriteLine();
        }

        private void WriteUIntCold(StreamWriter writer, uint value)
        {
            if (value.TryFormat(_formatBuffer.AsSpan(), out int written, default, CultureInfo.InvariantCulture))
                writer.Write(_formatBuffer, 0, written);
            else
                writer.Write('0');
        }

        private void WriteUShortCold(StreamWriter writer, ushort value)
        {
            if (value.TryFormat(_formatBuffer.AsSpan(), out int written, default, CultureInfo.InvariantCulture))
                writer.Write(_formatBuffer, 0, written);
            else
                writer.Write('0');
        }

        private void WriteFloatCold(StreamWriter writer, float value, string format)
        {
            if (!math.isfinite(value))
            {
                writer.Write('0');
                return;
            }

            if (value.TryFormat(_formatBuffer.AsSpan(), out int written, format.AsSpan(), CultureInfo.InvariantCulture))
                writer.Write(_formatBuffer, 0, written);
            else
                writer.Write('0');
        }

        private static void WriteStateNameCold(StreamWriter writer, ushort state)
        {
            switch ((QAWatchdogState1424)state)
            {
                case QAWatchdogState1424.ColdStart:
                    writer.Write("ColdStart");
                    break;
                case QAWatchdogState1424.WaitBootstrap:
                    writer.Write("WaitBootstrap");
                    break;
                case QAWatchdogState1424.WaitMainMenu:
                    writer.Write("WaitMainMenu");
                    break;
                case QAWatchdogState1424.InvokeMenuStart:
                    writer.Write("InvokeMenuStart");
                    break;
                case QAWatchdogState1424.WaitWorld:
                    writer.Write("WaitWorld");
                    break;
                case QAWatchdogState1424.Simulation:
                    writer.Write("Simulation");
                    break;
                case QAWatchdogState1424.Completed:
                    writer.Write("Completed");
                    break;
                case QAWatchdogState1424.Failed:
                    writer.Write("Failed");
                    break;
                default:
                    writer.Write("Unknown");
                    break;
            }
        }

        private static void EnsureDirectoryCold(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (_runActive && currentService != null)
                        RegisterTickLanesCold();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    if (_runActive)
                        EnsureStorageCold();
                    break;
                case GlobalRegistryServiceSlot.Scene:
                    _sceneService = currentService as ISceneService;
                    break;
            }
        }

        private void RegisterTickLanesCold()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_tickRegistered)
                _tickRegistered = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
            if (!_coldRegistered)
                _coldRegistered = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);
            if (!_lateRegistered)
                _lateRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void UnregisterTickLanesCold()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _tickRegistered = false;
            }

            if (_coldRegistered)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);
                _coldRegistered = false;
            }

            if (_lateRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _lateRegistered = false;
            }
        }

        private void TryRegisterHotSwapListenerCold()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListenerCold()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RegisterQuitHookCold()
        {
            if (_quitHookRegistered)
                return;

            Application.quitting += HandleApplicationQuittingCold;
            _quitHookRegistered = true;
        }

        private void UnregisterQuitHookCold()
        {
            if (!_quitHookRegistered)
                return;

            Application.quitting -= HandleApplicationQuittingCold;
            _quitHookRegistered = false;
        }

        private void HandleApplicationQuittingCold()
        {
            if (!_runActive || _terminalExportWritten)
                return;

            if (_state != QAWatchdogState1424.Completed)
            {
                _state = QAWatchdogState1424.Failed;
                if (_failReason == QAWatchdogFailReason1424.None)
                    _failReason = QAWatchdogFailReason1424.NativeSentinelLeak;
            }

            _terminalExportQueued = true;
            CoreDeterminismSignals.ClearInputOverride();
        }

        private void StopRunCold()
        {
            if (!_runActive && !_tickRegistered && !_coldRegistered && !_lateRegistered)
                return;

            CoreDeterminismSignals.ClearInputOverride();
            UnregisterTickLanesCold();
            TryUnregisterHotSwapListenerCold();
            DisposeRecordersCold();
            _runActive = false;
        }

        private void DisposeRecordersCold()
        {
            DisposeRecorder(ref _frameTimeRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            DisposeRecorder(ref _gfxUsedRecorder);
            DisposeRecorder(ref _batchesRecorder);
            DisposeRecorder(ref _setPassRecorder);
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();
            recorder = default;
        }
    }
}
#endif
