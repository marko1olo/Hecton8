using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

using DeterminismSignals = Hecton8.Core.CoreDeterminismSignals;

namespace Hecton8.QA
{
    public enum QAEnduranceTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3,
    }

    [DefaultExecutionOrder(-1200)]
    public sealed class QAEnduranceWatchdogBot :
        MonoBehaviour,
        IFastTickable,
        ILateFrameTickable,
        IOriginShiftListener,
        IGlobalRegistryHotSwapListener
    {
        private static int s_x001QAEnduranceWatchdogBotSignalPushDropCount;
        private const string AgentId = "QA_WATCHDOG_BOT";
        private const string CsvFileName = "QA_Endurance_Log.csv";
        private const string ResultFileName = "QAEnduranceResult_QA_WATCHDOG_BOT.txt";
        private const string AutoRunFlagPath = "Temp/H8_QA_LEGACY_ENDURANCE.flag";
        private const string SaveSlotName = "qa_endurance_10km";
        private const int BlackBoxCapacity = 300;
        private const int CsvQueueCapacity = 256;
        private const int CsvLineCapacity = 384;
        private const int CsvByteCapacity = 768;
        private const int FpsWindowCapacity = 64;
        private const int MinimumFrameBeforeSampling = 2;
        private const float DefaultTargetDistanceMeters = 10000f;
        private const float DefaultPdaIntervalMeters = 500f;
        private const float DefaultSaveIntervalMeters = 2000f;
        private const float DefaultCsvIntervalMeters = 1000f;
        private const float DefaultStuckSeconds = 5f;
        private const float DefaultStuckSpeedMetersPerSecond = 0.1f;
        private const float DefaultTrapLiftMeters = 5f;
        private const float MemoryLeakWindowMeters = 5000f;
        private const long MemoryLeakCriticalBytes = 100L * 1024L * 1024L;
        private const uint SourceHash = 0x51415744u;
        internal const uint EventHashNone = 0u;
        internal const uint EventHashStart = 0x53544152u;
        internal const uint EventHashCsvSample = 0x43535631u;
        internal const uint EventHashPdaRadar = 0x50444152u;
        internal const uint EventHashSaveRequest = 0x53415645u;
        internal const uint EventHashPhysicsTrap = 0x54524150u;
        internal const uint EventHashLeakCritical = 0x4C45414Bu;
        internal const uint EventHashOriginShift = 0x41555053u;
        internal const uint EventHashCrash = 0x43525348u;
        internal const uint EventHashComplete = 0x444F4E45u;
        private const SystemID OwnerSystemId = SystemID.QAEndurance;
        private static readonly char[] ResultFloatFormat = { 'F', '3' }; // COLD ALLOC: char[2] - terminal result float format - owner: QAEnduranceWatchdogBot

        private static QAEnduranceWatchdogBot _activeInstance;
        private static bool _autoRunBotCreated;

        [SerializeField] private bool runOnEnable;
        [SerializeField] private QAEnduranceTier tier = QAEnduranceTier.Low;
        [SerializeField] private float targetDistanceMeters = DefaultTargetDistanceMeters;
        [SerializeField] private float pdaIntervalMeters = DefaultPdaIntervalMeters;
        [SerializeField] private float saveIntervalMeters = DefaultSaveIntervalMeters;
        [SerializeField] private float csvIntervalMeters = DefaultCsvIntervalMeters;
        [SerializeField] private float stuckSeconds = DefaultStuckSeconds;
        [SerializeField] private float stuckSpeedMetersPerSecond = DefaultStuckSpeedMetersPerSecond;
        [SerializeField] private float trapLiftMeters = DefaultTrapLiftMeters;
        [SerializeField] private int pdaRadarTabIndex = 0;

        private QAEnduranceCsvWriter _csvWriter;
        private IDataVault _dataVault;
        private ISaveService _saveService;
        private ISaveService _queuedSaveService;
        private IPhysicsService _physicsService;
        private VaultGenerationHandle<QAEnduranceBlackBoxEntry> _blackBoxHandle;
        private AbsoluteUniversePosition _lastAup;
        private AbsoluteUniversePosition _currentAup;
        private AbsoluteUniversePosition _pendingPdaRadarAup;
        private float3 _currentRuntimePosition;
        private float3 _currentVelocity;
        private long _memoryWindowStartBytes;
        private long _lastTotalMemoryBytes;
        private long _lastManagedMemoryBytes;
        private long _lastGraphicsDriverBytes;
        private float _distanceMeters;
        private float _nextCsvDistance;
        private float _nextPdaDistance;
        private float _nextSaveDistance;
        private float _nextMemoryWindowDistance;
        private float _stuckTimerSeconds;
        private float _fpsAccumulated;
        private float _resolvedQualityWeight01;
        private int _fpsSampleCount;
        private int _blackBoxCursor;
        private int _blackBoxCount;
        private int _originShiftCount;
        private int _trapCount;
        private int _saveRequestCount;
        private int _csvDropCount;
        private int _lastFrame;
        private bool _hasLastAup;
        private bool _automationInputPublished;
        private bool _tickRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _originListenerRegistered;
        private Rigidbody _pendingRecoveryBody;
        private Vector3 _pendingRecoveryBodyPosition;
        private Transform _pendingRecoveryTransform;
        private Vector3 _pendingRecoveryTransformPosition;
        private bool _hasPendingRecoveryBodyPosition;
        private bool _hasPendingRecoveryTransformPosition;
        private bool _pdaRadarRequestQueued;
        private bool _instanceAccepted;
        private bool _active;
        private bool _pdaOpen;
        private bool _saveInFlight;
        private bool _saveRequestQueued;
        private bool _completed;
        private bool _faulted;
        private readonly char[] _resultFormatBuffer = new char[32]; // COLD ALLOC: char[32] - terminal result numeric format scratch - owner: QAEnduranceWatchdogBot
        private string _csvPath;
        private string _resultPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeInstance = null;
            _autoRunBotCreated = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapFromCommandLine()
        {
            if (!ShouldAutoRun())
                return;

            SceneManager.sceneLoaded -= HandleSceneLoadedForAutoRun;
            SceneManager.sceneLoaded += HandleSceneLoadedForAutoRun;
            TryCreateAutoRunBot();
        }

        private static void HandleSceneLoadedForAutoRun(Scene scene, LoadSceneMode mode)
        {
            TryCreateAutoRunBot();
        }

        private static void TryCreateAutoRunBot()
        {
            if (!ShouldAutoRun() || _activeInstance != null || _autoRunBotCreated)
                return;

            _autoRunBotCreated = true;
            GameObject root = new GameObject("[QAEnduranceWatchdogBot]"); // COLD ALLOC: GameObject[1] — autorun QA harness root — owner: QAEnduranceWatchdogBot
            root.SetActive(false);
            QAEnduranceWatchdogBot bot = root.AddComponent<QAEnduranceWatchdogBot>(); // COLD ALLOC: QAEnduranceWatchdogBot[1] — autorun QA component — owner: QAEnduranceWatchdogBot
            bot.runOnEnable = true;
            bot.tier = ResolveTierFromCommandLine();
            Object.DontDestroyOnLoad(root);
            root.SetActive(true);
            SceneManager.sceneLoaded -= HandleSceneLoadedForAutoRun;
        }

        private static bool ShouldAutoRun()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "-h8QaLegacyEndurance", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (string.Equals(System.Environment.GetEnvironmentVariable("H8_QA_LEGACY_ENDURANCE"), "1", StringComparison.Ordinal))
                return true;

            return File.Exists(ResolveProjectPath(AutoRunFlagPath));
        }

        private static QAEnduranceTier ResolveTierFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-h8QaTier", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryResolveTierToken(args[i + 1], out QAEnduranceTier parsed))
                    return parsed;
            }

            return QAEnduranceTier.Low;
        }

        private static bool TryResolveTierToken(string value, out QAEnduranceTier tier)
        {
            tier = QAEnduranceTier.Low;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (string.Equals(value, "0", StringComparison.Ordinal) ||
                string.Equals(value, "low", StringComparison.OrdinalIgnoreCase))
            {
                tier = QAEnduranceTier.Low;
                return true;
            }

            if (string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "mid", StringComparison.OrdinalIgnoreCase))
            {
                tier = QAEnduranceTier.Middle;
                return true;
            }

            if (string.Equals(value, "2", StringComparison.Ordinal) ||
                string.Equals(value, "high", StringComparison.OrdinalIgnoreCase))
            {
                tier = QAEnduranceTier.High;
                return true;
            }

            if (string.Equals(value, "3", StringComparison.Ordinal) ||
                string.Equals(value, "ultra", StringComparison.OrdinalIgnoreCase))
            {
                tier = QAEnduranceTier.Ultra;
                return true;
            }

            return false;
        }

        private void Awake()
        {
            if (_activeInstance != null && !ReferenceEquals(_activeInstance, this))
            {
                enabled = false;
                return;
            }

            _activeInstance = this;
            _instanceAccepted = true;
            ApplyTierDefaults();
            ResolveArtifactPaths();
            EnsureBlackBox();
        }

        private void OnEnable()
        {
            if (!_instanceAccepted)
                return;

            Application.logMessageReceived -= HandleLogMessage;
            Application.logMessageReceived += HandleLogMessage;

            if (runOnEnable)
                BeginRun();
        }

        private void OnDisable()
        {
            if (!_instanceAccepted)
                return;

            Application.logMessageReceived -= HandleLogMessage;
            StopRun(false, EventHashNone);
        }

        private void OnDestroy()
        {
            if (_instanceAccepted)
            {
                StopRun(false, EventHashNone);
                if (ReferenceEquals(_activeInstance, this))
                    _activeInstance = null;
            }

            ReleaseBlackBox();
        }

        public void BeginRun()
        {
            BeginRunCold();
        }

        private void BeginRunCold()
        {
            if (_active)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));
            _csvWriter = new QAEnduranceCsvWriter(_csvPath, CsvQueueCapacity, CsvLineCapacity, CsvByteCapacity); // COLD ALLOC: QAEnduranceCsvWriter[1] — decoupled CSV writer — owner: QAEnduranceWatchdogBot
            _csvWriter.StartCold();
            _memoryWindowStartBytes = Profiler.GetTotalAllocatedMemoryLong();
            _lastTotalMemoryBytes = _memoryWindowStartBytes;
            _lastManagedMemoryBytes = Profiler.GetMonoUsedSizeLong();
            _lastGraphicsDriverBytes = ResolveGraphicsDriverBytes();
            _resolvedQualityWeight01 = ResolveGlobalQualityWeight01();
            _nextCsvDistance = ResolveCsvIntervalMeters();
            _nextPdaDistance = pdaIntervalMeters;
            _nextSaveDistance = saveIntervalMeters;
            _nextMemoryWindowDistance = MemoryLeakWindowMeters;
            _distanceMeters = 0f;
            _fpsAccumulated = 0f;
            _fpsSampleCount = 0;
            _blackBoxCursor = 0;
            _blackBoxCount = 0;
            EnsureBlackBox();
            _saveService = GlobalRegistry.Save;
            _physicsService = GlobalRegistry.Physics;
            _originShiftCount = 0;
            _trapCount = 0;
            _saveRequestCount = 0;
            _csvDropCount = 0;
            _queuedSaveService = null;
            _pdaRadarRequestQueued = false;
            _saveRequestQueued = false;
            _saveInFlight = false;
            _lastFrame = -1;
            _hasLastAup = false;
            _completed = false;
            _faulted = false;
            _active = true;

            TryRegisterHotSwapListener();
            RegisterTickLanes();
            RegisterOriginListener();
            PublishAutomationInput();
            WriteBlackBox(EventHashStart);
            EnqueueCsvRecord(EventHashStart);
        }

        public void FastTick(float deltaTime)
        {
            if (!_active || _faulted || _completed)
                return;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (currentFrame < MinimumFrameBeforeSampling || _lastFrame == currentFrame)
                return;

            _lastFrame = currentFrame;
            _resolvedQualityWeight01 = ResolveGlobalQualityWeight01();
            PublishAutomationInput();

            if (!TryResolvePlayerState(out PlayerRuntimeContext runtimeContext, out PlayerMovementRuntimeState movementState))
                return;

            float safeDeltaTime = math.max(deltaTime, 0.000001f);
            _currentAup = movementState.PredictedAup;
            _currentRuntimePosition = movementState.WorldPosition;
            _currentVelocity = movementState.Velocity;
            _lastTotalMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            _lastManagedMemoryBytes = Profiler.GetMonoUsedSizeLong();
            _lastGraphicsDriverBytes = ResolveGraphicsDriverBytes();

            if (HasInvalidState(in movementState))
            {
                FaultAndDump(EventHashCrash);
                return;
            }

            AccumulateFps(safeDeltaTime);
            AccumulateDistance(in _currentAup);
            CheckStuck(runtimeContext, in movementState, safeDeltaTime);
            WriteBlackBox(EventHashNone);

            if (_distanceMeters >= _nextCsvDistance)
                SampleCsv(EventHashCsvSample);

            if (_distanceMeters >= _nextPdaDistance)
                QueuePdaRadar(in _currentAup);

            if (_distanceMeters >= _nextSaveDistance)
                RequestSaveIfAvailable();

            if (_distanceMeters >= _nextMemoryWindowDistance)
                CheckMemoryWindow();

            if (_distanceMeters >= targetDistanceMeters)
                CompleteRun();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _originShiftCount++;
            WriteBlackBox(EventHashOriginShift);
            EnqueueCsvRecord(EventHashOriginShift);
        }

        private void ApplyTierDefaults()
        {
            targetDistanceMeters = math.max(1f, targetDistanceMeters <= 0f ? DefaultTargetDistanceMeters : targetDistanceMeters);
            pdaIntervalMeters = math.max(1f, pdaIntervalMeters <= 0f ? DefaultPdaIntervalMeters : pdaIntervalMeters);
            saveIntervalMeters = math.max(1f, saveIntervalMeters <= 0f ? DefaultSaveIntervalMeters : saveIntervalMeters);
            csvIntervalMeters = math.max(1f, csvIntervalMeters <= 0f ? DefaultCsvIntervalMeters : csvIntervalMeters);
            stuckSeconds = math.max(0.1f, stuckSeconds <= 0f ? DefaultStuckSeconds : stuckSeconds);
            stuckSpeedMetersPerSecond = math.max(0.001f, stuckSpeedMetersPerSecond <= 0f ? DefaultStuckSpeedMetersPerSecond : stuckSpeedMetersPerSecond);
            trapLiftMeters = math.max(0.1f, trapLiftMeters <= 0f ? DefaultTrapLiftMeters : trapLiftMeters);
        }

        private float ResolveCsvIntervalMeters()
        {
            float minimumIntervalMeters = math.min(csvIntervalMeters, 250f);
            return math.max(1f, math.lerp(csvIntervalMeters, minimumIntervalMeters, ResolveEnduranceQuality01()));
        }

        private float ResolveEnduranceQuality01()
        {
            float tierHint = math.saturate((float)tier * 0.33333334f);
            return math.saturate(math.lerp(_resolvedQualityWeight01, tierHint, 0.15f));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0f;
        }

        private void ResolveArtifactPaths()
        {
            string logRoot = ResolveProjectPath("Docs/AgentLogs");
            _csvPath = Path.Combine(logRoot, CsvFileName);
            _resultPath = Path.Combine(logRoot, ResultFileName);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(projectRoot, "Assets")) && !string.IsNullOrEmpty(Application.dataPath))
                projectRoot = Directory.GetParent(Application.dataPath).FullName;

            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private void RegisterTickLanes()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_tickRegistered || _lateFrameRegistered)
                UnregisterTickLanes();

            if (!_tickRegistered)
                _tickRegistered = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            if (!_tickRegistered || !_lateFrameRegistered)
                UnregisterTickLanes();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null || !_active || _faulted || _completed || !isActiveAndEnabled)
                        return;

                    UnregisterTickLanes();
                    RegisterTickLanes();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseBlackBox();
                    _dataVault = currentService as IDataVault;
                    if (_active && !_faulted && !_completed)
                        EnsureBlackBox();
                    break;
                case GlobalRegistryServiceSlot.Save:
                    _saveService = currentService as ISaveService;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
            }
        }

        public void LateFrameTick()
        {
            if (_hasPendingRecoveryBodyPosition)
            {
                _hasPendingRecoveryBodyPosition = false;
                Rigidbody body = _pendingRecoveryBody;
                Vector3 position = _pendingRecoveryBodyPosition;
                _pendingRecoveryBody = null;
                if (body != null)
                {
                    body.position = position;
                    IPhysicsService physics = _physicsService;
                    if (physics != null)
                    {
                        physics.QueueLinearVelocitySet(body, Vector3.zero, wake: false);
                        physics.QueueAngularVelocitySet(body, Vector3.zero, wake: false);
                    }

                    body.WakeUp();
                }
            }

            if (_hasPendingRecoveryTransformPosition)
            {
                _hasPendingRecoveryTransformPosition = false;
                if (_pendingRecoveryTransform != null)
                    _pendingRecoveryTransform.position = _pendingRecoveryTransformPosition;
                _pendingRecoveryTransform = null;
            }

            if (_pdaRadarRequestQueued)
                FlushQueuedPdaRadarLate();

            if (_saveRequestQueued)
                StartQueuedSaveCold();
        }

        private void RegisterOriginListener()
        {
            if (_originListenerRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originListenerRegistered = true;
        }

        private void PublishAutomationInput()
        {
            PlayerInputState state = default;
            state.MoveDelta.x = 0f;
            state.MoveDelta.y = 1f;
            state.LookDelta.x = 0f;
            state.LookDelta.y = -0.012f;
            state.VerticalDelta = 0.15f;
            state.ActionsBitmask = (uint)PlayerInputAction.Sprint;
            DeterminismSignals.TryPublishInputOverride(in state, Hecton8.Core.SystemDispatcher.CurrentFrameId);
            _automationInputPublished = true;
        }

        private static bool TryResolvePlayerState(
            out PlayerRuntimeContext runtimeContext,
            out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out runtimeContext) ||
                runtimeContext == null ||
                !runtimeContext.IsBound)
            {
                return false;
            }

            movementState = runtimeContext.MovementState;
            return true;
        }

        private static bool HasInvalidState(in PlayerMovementRuntimeState state)
        {
            return !math.all(math.isfinite(state.WorldPosition)) ||
                   !math.all(math.isfinite(state.PredictedWorldPosition)) ||
                   !math.all(math.isfinite(state.Velocity)) ||
                   !math.all(math.isfinite(state.Forward)) ||
                   !math.all(math.isfinite(state.CameraForward));
        }

        private void AccumulateFps(float deltaTime)
        {
            float fps = math.min(1000f, 1f / math.max(deltaTime, 0.000001f));
            _fpsAccumulated += fps;
            _fpsSampleCount++;
            if (_fpsSampleCount > FpsWindowCapacity)
            {
                _fpsAccumulated *= 0.5f;
                _fpsSampleCount = FpsWindowCapacity / 2;
            }
        }

        private float ResolveAverageFps()
        {
            return _fpsSampleCount > 0 ? _fpsAccumulated / _fpsSampleCount : 0f;
        }

        private void AccumulateDistance(in AbsoluteUniversePosition aup)
        {
            if (!_hasLastAup)
            {
                _lastAup = aup;
                _hasLastAup = true;
                return;
            }

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in _lastAup, in aup);
            if (distanceSq > 0d && distanceSq < 1000000d && math.isfinite(distanceSq))
                _distanceMeters += (float)math.sqrt(distanceSq);

            _lastAup = aup;
        }

        private void CheckStuck(
            PlayerRuntimeContext runtimeContext,
            in PlayerMovementRuntimeState movementState,
            float deltaTime)
        {
            float speedSq = math.lengthsq(movementState.Velocity);
            float thresholdSq = stuckSpeedMetersPerSecond * stuckSpeedMetersPerSecond;
            if (speedSq > thresholdSq)
            {
                _stuckTimerSeconds = 0f;
                return;
            }

            _stuckTimerSeconds += deltaTime;
            if (_stuckTimerSeconds < stuckSeconds)
                return;

            _stuckTimerSeconds = 0f;
            _trapCount++;
            PublishCompliance(EventHashPhysicsTrap, 2);
            RecoverFromTrap(runtimeContext);
            WriteBlackBox(EventHashPhysicsTrap);
            EnqueueCsvRecord(EventHashPhysicsTrap);
        }

        private void RecoverFromTrap(PlayerRuntimeContext runtimeContext)
        {
            if (runtimeContext == null)
                return;

            Vector3 offset = Vector3.up * trapLiftMeters;
            Rigidbody body = runtimeContext.PlayerRigidbody;
            if (body != null)
            {
                Vector3 nextPosition = body.position + offset;
                if (!IsFinite(nextPosition))
                    return;

                QueueRecoveryBodyPosition(body, nextPosition);
                if (runtimeContext.PlayerTransform != null)
                    QueueRecoveryTransformPosition(runtimeContext.PlayerTransform, nextPosition);
                return;
            }

            Transform playerTransform = runtimeContext.PlayerTransform;
            if (playerTransform == null)
                return;

            Vector3 nextTransformPosition = playerTransform.position + offset;
            if (IsFinite(nextTransformPosition))
                QueueRecoveryTransformPosition(playerTransform, nextTransformPosition);
        }

        private void QueueRecoveryTransformPosition(Transform target, Vector3 position)
        {
            _pendingRecoveryTransform = target;
            _pendingRecoveryTransformPosition = position;
            _hasPendingRecoveryTransformPosition = true;
        }

        private void QueueRecoveryBodyPosition(Rigidbody target, Vector3 position)
        {
            _pendingRecoveryBody = target;
            _pendingRecoveryBodyPosition = position;
            _hasPendingRecoveryBodyPosition = true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private void QueuePdaRadar(in AbsoluteUniversePosition aup)
        {
            if (_pdaRadarRequestQueued)
                return;

            _pendingPdaRadarAup = aup;
            _pdaRadarRequestQueued = true;
            _nextPdaDistance += pdaIntervalMeters;
        }

        private void FlushQueuedPdaRadarLate()
        {
            _pdaRadarRequestQueued = false;
            AbsoluteUniversePosition aup = _pendingPdaRadarAup;
            TogglePdaRadarLate(in aup);
        }

        private void TogglePdaRadarLate(in AbsoluteUniversePosition aup)
        {
            if (_pdaOpen)
                ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateClosePDA());
            else
                ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateOpenPDATab(pdaRadarTabIndex));

            _pdaOpen = !_pdaOpen;
            SonarPingSignal sonar = default;
            sonar.PositionAup = aup;
            sonar.RadiusMeters = ResolveRadarRadius();
            sonar.Intensity01 = ResolveRadarIntensity();
            sonar.SourceId = SourceHash;
            sonar.Flags = 1;
            SignalBus<SonarPingSignal>.TryPushTracked(in sonar, ref s_x001QAEnduranceWatchdogBotSignalPushDropCount);
            WriteBlackBox(EventHashPdaRadar);
            EnqueueCsvRecord(EventHashPdaRadar);
        }

        private float ResolveRadarRadius()
        {
            return math.lerp(80f, 180f, ResolveEnduranceQuality01());
        }

        private float ResolveRadarIntensity()
        {
            return math.lerp(0.55f, 1f, ResolveEnduranceQuality01());
        }

        private void RequestSaveIfAvailable()
        {
            _nextSaveDistance += saveIntervalMeters;
            if (_saveInFlight || _saveRequestQueued)
                return;

            ISaveService save = _saveService;
            if (save == null || !save.IsInitialized || save.IsBusy)
                return;

            _queuedSaveService = save;
            _saveRequestQueued = true;
            _saveRequestCount++;
            WriteBlackBox(EventHashSaveRequest);
            EnqueueCsvRecord(EventHashSaveRequest);
        }

        private void StartQueuedSaveCold()
        {
            _saveRequestQueued = false;
            ISaveService save = _queuedSaveService;
            _queuedSaveService = null;
            if (save == null || !save.IsInitialized || save.IsBusy)
                return;

            _saveInFlight = true;
            _ = SaveAsync(save);
        }

        private async Awaitable SaveAsync(ISaveService save)
        {
            try
            {
                await save.SaveGameAsync(SaveSlotName);
            }
            catch
            {
                FaultAndDump(EventHashCrash);
            }
            finally
            {
                _saveInFlight = false;
            }
        }

        private void SampleCsv(uint eventHash)
        {
            while (_distanceMeters >= _nextCsvDistance)
                _nextCsvDistance += ResolveCsvIntervalMeters();

            WriteBlackBox(eventHash);
            EnqueueCsvRecord(eventHash);
        }

        private void CheckMemoryWindow()
        {
            long deltaBytes = _lastTotalMemoryBytes - _memoryWindowStartBytes;
            if (deltaBytes > MemoryLeakCriticalBytes)
            {
                PublishCompliance(EventHashLeakCritical, 3);
                WriteBlackBox(EventHashLeakCritical);
                EnqueueCsvRecord(EventHashLeakCritical);
            }

            _memoryWindowStartBytes = _lastTotalMemoryBytes;
            _nextMemoryWindowDistance += MemoryLeakWindowMeters;
        }

        private void CompleteRun()
        {
            _completed = true;
            WriteBlackBox(EventHashComplete);
            EnqueueCsvRecord(EventHashComplete);
            WriteResultFileCold(0, EventHashComplete);
            StopRun(true, EventHashComplete);
        }

        private void FaultAndDump(uint eventHash)
        {
            if (_faulted)
                return;

            _faulted = true;
            PublishCompliance(eventHash, 4);
            WriteBlackBox(eventHash);
            EnqueueCsvRecord(eventHash);
            RetainBlackBoxInVaultCold();
            WriteResultFileCold(1, eventHash);
            StopRun(true, eventHash);
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_active)
                return;

            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                FaultAndDump(EventHashCrash);
        }

        private void StopRun(bool flush, uint eventHash)
        {
            DeterminismSignals.ClearInputOverride();

            UnregisterTickLanes();
            TryUnregisterHotSwapListener();

            _hasPendingRecoveryTransformPosition = false;
            _hasPendingRecoveryBodyPosition = false;
            _pendingRecoveryTransform = null;
            _pendingRecoveryBody = null;
            _pdaRadarRequestQueued = false;
            _saveRequestQueued = false;
            _queuedSaveService = null;

            if (_originListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originListenerRegistered = false;
            }

            _active = false;

            if (_csvWriter != null)
            {
                if (flush)
                    _csvWriter.FlushCold();
                _csvWriter.Dispose();
                _csvWriter = null;
            }
        }

        private void UnregisterTickLanes()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _tickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _lateFrameRegistered = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void PublishCompliance(uint ruleHash, byte severity)
        {
            ComplianceViolationSignal signal = default;
            signal.RuleHash = ruleHash;
            signal.SystemHash = SourceHash;
            signal.ContextHash = AgentIdHash;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Severity = severity;
            signal.Flags = 1;
            SignalBus<ComplianceViolationSignal>.TryPushTracked(in signal, ref s_x001QAEnduranceWatchdogBotSignalPushDropCount);
        }

        private void EnsureBlackBox()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsVaultHandleCreated(in _blackBoxHandle) &&
                vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<QAEnduranceBlackBoxEntry>.ReadOnly buffer) &&
                buffer.IsCreated &&
                buffer.Length >= BlackBoxCapacity)
            {
                return;
            }

            _blackBoxHandle = vault.EnsureGenerationHandle<QAEnduranceBlackBoxEntry>(
                BufferID.QAEnduranceBlackBoxRing,
                BlackBoxCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private void ReleaseBlackBox()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in _blackBoxHandle))
                vault.ReleaseBuffer(in _blackBoxHandle);

            _blackBoxHandle = default;
            _blackBoxCursor = 0;
            _blackBoxCount = 0;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static uint AgentIdHash => 0x51415742u;

        private void WriteBlackBox(uint eventHash)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in _blackBoxHandle) ||
                !vault.TryAcquireWriteLock(in _blackBoxHandle, OwnerSystemId, out NativeArray<QAEnduranceBlackBoxEntry> blackBox))
            {
                return;
            }

            try
            {
                if (!blackBox.IsCreated || blackBox.Length < BlackBoxCapacity)
                    return;

                QAEnduranceBlackBoxEntry entry = default;
                entry.Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
                entry.DistanceMeters = _distanceMeters;
                entry.RuntimePosition = _currentRuntimePosition;
                entry.Velocity = _currentVelocity;
                entry.Aup = _currentAup;
                entry.TotalMemoryBytes = _lastTotalMemoryBytes;
                entry.ManagedMemoryBytes = _lastManagedMemoryBytes;
                entry.GraphicsDriverBytes = _lastGraphicsDriverBytes;
                entry.AverageFps = ResolveAverageFps();
                entry.EventHash = eventHash;
                entry.Flags = BuildBlackBoxFlags();

                int index = math.clamp(_blackBoxCursor, 0, BlackBoxCapacity - 1);
                blackBox[index] = entry;
                _blackBoxCursor = (index + 1) % BlackBoxCapacity;
                if (_blackBoxCount < BlackBoxCapacity)
                    _blackBoxCount++;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackBoxHandle, OwnerSystemId);
            }
        }

        private uint BuildBlackBoxFlags()
        {
            uint flags = 0u;
            if (_automationInputPublished)
                flags |= 1u;
            if (_pdaOpen)
                flags |= 1u << 1;
            if (_saveInFlight || _saveRequestQueued)
                flags |= 1u << 2;
            if (_faulted)
                flags |= 1u << 3;
            return flags;
        }

        private void EnqueueCsvRecord(uint eventHash)
        {
            if (_csvWriter == null)
                return;

            QAEnduranceCsvRecord record = default;
            record.Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
            record.DistanceMeters = _distanceMeters;
            record.AverageFps = ResolveAverageFps();
            record.TotalMemoryBytes = _lastTotalMemoryBytes;
            record.ManagedMemoryBytes = _lastManagedMemoryBytes;
            record.GraphicsDriverBytes = _lastGraphicsDriverBytes;
            record.RuntimeX = _currentRuntimePosition.x;
            record.RuntimeY = _currentRuntimePosition.y;
            record.RuntimeZ = _currentRuntimePosition.z;
            record.VelocityMagnitude = math.length(_currentVelocity);
            record.OriginShiftCount = _originShiftCount;
            record.TrapCount = _trapCount;
            record.SaveRequestCount = _saveRequestCount;
            record.CsvDropCount = _csvDropCount;
            record.Tier = (byte)tier;
            record.QualityByte = (byte)math.round(ResolveEnduranceQuality01() * 255f);
            record.EventHash = eventHash;

            if (!_csvWriter.TryEnqueue(in record))
                _csvDropCount++;
        }

        private void RetainBlackBoxInVaultCold()
        {
            // Source-only proof mode: the fixed 300-frame black-box ring remains
            // in GlobalDataVault. Disk binary dumps are intentionally disabled.
        }

        private void WriteResultFileCold(int exitCode, uint eventHash)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
            using (StreamWriter writer = new StreamWriter(_resultPath, false)) // COLD ALLOC: StreamWriter[1] - terminal exit signal - owner: QAEnduranceWatchdogBot
            {
                writer.Write("agent=");
                writer.Write(AgentId);
                writer.WriteLine();
                writer.Write("exitCode=");
                WriteIntCold(writer, exitCode);
                writer.WriteLine();
                writer.Write("eventHash=");
                WriteUIntCold(writer, eventHash);
                writer.WriteLine();
                writer.Write("distanceMeters=");
                WriteFloatCold(writer, _distanceMeters);
                writer.WriteLine();
                writer.Write("originShifts=");
                WriteIntCold(writer, _originShiftCount);
                writer.WriteLine();
                writer.Write("traps=");
                WriteIntCold(writer, _trapCount);
                writer.WriteLine();
                writer.Write("saveRequests=");
                WriteIntCold(writer, _saveRequestCount);
                writer.WriteLine();
                writer.Write("csvDrops=");
                WriteIntCold(writer, _csvDropCount);
                writer.WriteLine();
            }
        }

        private void WriteIntCold(StreamWriter writer, int value)
        {
            Span<char> buffer = _resultFormatBuffer.AsSpan();
            if (value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture))
                writer.Write(_resultFormatBuffer, 0, written);
        }

        private void WriteUIntCold(StreamWriter writer, uint value)
        {
            Span<char> buffer = _resultFormatBuffer.AsSpan();
            if (value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture))
                writer.Write(_resultFormatBuffer, 0, written);
        }

        private void WriteFloatCold(StreamWriter writer, float value)
        {
            Span<char> buffer = _resultFormatBuffer.AsSpan();
            if (value.TryFormat(buffer, out int written, ResultFloatFormat, CultureInfo.InvariantCulture))
                writer.Write(_resultFormatBuffer, 0, written);
        }

        private static long ResolveGraphicsDriverBytes()
        {
            long value = Profiler.GetAllocatedMemoryForGraphicsDriver();
            return value > 0L ? value : 0L;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct QAEnduranceBlackBoxEntry
        {
            [FieldOffset(0)] public int Frame;
            [FieldOffset(4)] public float DistanceMeters;
            [FieldOffset(8)] public float3 RuntimePosition;
            [FieldOffset(20)] public float3 Velocity;
            [FieldOffset(32)] public AbsoluteUniversePosition Aup;
            [FieldOffset(80)] public long TotalMemoryBytes;
            [FieldOffset(88)] public long ManagedMemoryBytes;
            [FieldOffset(96)] public long GraphicsDriverBytes;
            [FieldOffset(104)] public float AverageFps;
            [FieldOffset(108)] public uint EventHash;
            [FieldOffset(112)] public uint Flags;
            [FieldOffset(116)] private uint _pad0;
            [FieldOffset(120)] private ulong _pad1;
        }

    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct QAEnduranceCsvRecord
    {
        [FieldOffset(0)] public int Frame;
        [FieldOffset(4)] public float DistanceMeters;
        [FieldOffset(8)] public float AverageFps;
        [FieldOffset(12)] private uint _pad0;
        [FieldOffset(16)] public long TotalMemoryBytes;
        [FieldOffset(24)] public long ManagedMemoryBytes;
        [FieldOffset(32)] public long GraphicsDriverBytes;
        [FieldOffset(40)] public float RuntimeX;
        [FieldOffset(44)] public float RuntimeY;
        [FieldOffset(48)] public float RuntimeZ;
        [FieldOffset(52)] public float VelocityMagnitude;
        [FieldOffset(56)] public int OriginShiftCount;
        [FieldOffset(60)] public int TrapCount;
        [FieldOffset(64)] public int SaveRequestCount;
        [FieldOffset(68)] public int CsvDropCount;
        [FieldOffset(72)] public byte Tier;
        [FieldOffset(73)] public byte QualityByte;
        [FieldOffset(74)] private ushort _pad2;
        [FieldOffset(76)] public uint EventHash;
    }

    internal sealed class QAEnduranceCsvWriter : IDisposable
    {
        private static readonly char[] FloatFormat = { 'F', '3' }; // COLD ALLOC: char[2] — fixed float format token — owner: QAEnduranceCsvWriter
        private static readonly byte[] HeaderBytes = EncodeStaticAscii(
            "frame,distanceMeters,avgFps,totalMemoryBytes,managedMemoryBytes,graphicsDriverBytes,x,y,z,velocityMetersPerSecond,originShifts,traps,saveRequests,csvDrops,tier,qualityByte,eventToken,eventHash\n"); // COLD ALLOC: byte[headerLength] — static CSV header bytes — owner: QAEnduranceCsvWriter

        private readonly QAEnduranceCsvRecord[] _records;
        private readonly char[] _lineChars;
        private readonly byte[] _lineBytes;
        private readonly string _path;
        private int _writeIndex;
        private int _count;

        public QAEnduranceCsvWriter(string path, int capacity, int lineCapacity, int byteCapacity)
        {
            _path = path;
            _records = new QAEnduranceCsvRecord[math.max(4, capacity)]; // COLD ALLOC: QAEnduranceCsvRecord[capacity] — bounded CSV record ring — owner: QAEnduranceCsvWriter
            _lineChars = new char[math.max(128, lineCapacity)]; // COLD ALLOC: char[lineCapacity] — CSV line format buffer — owner: QAEnduranceCsvWriter
            _lineBytes = new byte[math.max(256, byteCapacity)]; // COLD ALLOC: byte[byteCapacity] — CSV ASCII write buffer — owner: QAEnduranceCsvWriter
        }

        public void StartCold()
        {
            _writeIndex = 0;
            _count = 0;
        }

        public bool TryEnqueue(in QAEnduranceCsvRecord record)
        {
            if (_count >= _records.Length)
                return false;

            _records[_writeIndex] = record;
            _writeIndex++;
            if (_writeIndex >= _records.Length)
                _writeIndex = 0;
            _count++;
            return true;
        }

        public void FlushCold()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using (FileStream stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096)) // COLD ALLOC: FileStream[1] — terminal CSV file sink — owner: QAEnduranceCsvWriter
            {
                stream.Write(HeaderBytes, 0, HeaderBytes.Length);
                for (int i = 0; i < _count; i++)
                    WriteRecord(stream, in _records[i]);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _count; i++)
                _records[i] = default;
            _writeIndex = 0;
            _count = 0;
        }

        private void WriteRecord(FileStream stream, in QAEnduranceCsvRecord record)
        {
            Span<char> chars = _lineChars;
            int cursor = 0;
            AppendInt(chars, ref cursor, record.Frame);
            AppendComma(chars, ref cursor);
            AppendFloat(chars, ref cursor, record.DistanceMeters);
            AppendComma(chars, ref cursor);
            AppendFloat(chars, ref cursor, record.AverageFps);
            AppendComma(chars, ref cursor);
            AppendLong(chars, ref cursor, record.TotalMemoryBytes);
            AppendComma(chars, ref cursor);
            AppendLong(chars, ref cursor, record.ManagedMemoryBytes);
            AppendComma(chars, ref cursor);
            AppendLong(chars, ref cursor, record.GraphicsDriverBytes);
            AppendComma(chars, ref cursor);
            AppendFloat(chars, ref cursor, record.RuntimeX);
            AppendComma(chars, ref cursor);
            AppendFloat(chars, ref cursor, record.RuntimeY);
            AppendComma(chars, ref cursor);
            AppendFloat(chars, ref cursor, record.RuntimeZ);
            AppendComma(chars, ref cursor);
            AppendFloat(chars, ref cursor, record.VelocityMagnitude);
            AppendComma(chars, ref cursor);
            AppendInt(chars, ref cursor, record.OriginShiftCount);
            AppendComma(chars, ref cursor);
            AppendInt(chars, ref cursor, record.TrapCount);
            AppendComma(chars, ref cursor);
            AppendInt(chars, ref cursor, record.SaveRequestCount);
            AppendComma(chars, ref cursor);
            AppendInt(chars, ref cursor, record.CsvDropCount);
            AppendComma(chars, ref cursor);
            AppendInt(chars, ref cursor, record.Tier);
            AppendComma(chars, ref cursor);
            AppendInt(chars, ref cursor, record.QualityByte);
            AppendComma(chars, ref cursor);
            AppendEventToken(chars, ref cursor, record.EventHash);
            AppendComma(chars, ref cursor);
            AppendUInt(chars, ref cursor, record.EventHash);
            AppendNewLine(chars, ref cursor);

            int byteCount = EncodeAscii(chars.Slice(0, cursor), _lineBytes);
            stream.Write(_lineBytes, 0, byteCount);
        }

        private static void AppendComma(Span<char> destination, ref int cursor)
        {
            destination[cursor++] = ',';
        }

        private static void AppendNewLine(Span<char> destination, ref int cursor)
        {
            destination[cursor++] = '\n';
        }

        private static void AppendInt(Span<char> destination, ref int cursor, int value)
        {
            if (value.TryFormat(destination.Slice(cursor), out int written, provider: CultureInfo.InvariantCulture))
                cursor += written;
        }

        private static void AppendUInt(Span<char> destination, ref int cursor, uint value)
        {
            if (value.TryFormat(destination.Slice(cursor), out int written, provider: CultureInfo.InvariantCulture))
                cursor += written;
        }

        private static void AppendLong(Span<char> destination, ref int cursor, long value)
        {
            if (value.TryFormat(destination.Slice(cursor), out int written, provider: CultureInfo.InvariantCulture))
                cursor += written;
        }

        private static void AppendFloat(Span<char> destination, ref int cursor, float value)
        {
            if (value.TryFormat(destination.Slice(cursor), out int written, FloatFormat, CultureInfo.InvariantCulture))
                cursor += written;
        }

        private static void AppendEventToken(Span<char> destination, ref int cursor, uint eventHash)
        {
            switch (eventHash)
            {
                case QAEnduranceWatchdogBot.EventHashStart:
                    AppendLiteral(destination, ref cursor, "START");
                    return;
                case QAEnduranceWatchdogBot.EventHashCsvSample:
                    AppendLiteral(destination, ref cursor, "CSV_SAMPLE");
                    return;
                case QAEnduranceWatchdogBot.EventHashPdaRadar:
                    AppendLiteral(destination, ref cursor, "PDA_RADAR");
                    return;
                case QAEnduranceWatchdogBot.EventHashSaveRequest:
                    AppendLiteral(destination, ref cursor, "SAVE_REQUEST");
                    return;
                case QAEnduranceWatchdogBot.EventHashPhysicsTrap:
                    AppendLiteral(destination, ref cursor, "PHYSICS_TRAP");
                    return;
                case QAEnduranceWatchdogBot.EventHashLeakCritical:
                    AppendLiteral(destination, ref cursor, "LEAK_CRITICAL");
                    return;
                case QAEnduranceWatchdogBot.EventHashOriginShift:
                    AppendLiteral(destination, ref cursor, "AUP_SHIFT");
                    return;
                case QAEnduranceWatchdogBot.EventHashCrash:
                    AppendLiteral(destination, ref cursor, "CRASH");
                    return;
                case QAEnduranceWatchdogBot.EventHashComplete:
                    AppendLiteral(destination, ref cursor, "COMPLETE");
                    return;
                default:
                    AppendLiteral(destination, ref cursor, "FRAME");
                    return;
            }
        }

        private static void AppendLiteral(Span<char> destination, ref int cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
                destination[cursor++] = value[i];
        }

        private static int EncodeAscii(ReadOnlySpan<char> source, byte[] destination)
        {
            int length = math.min(source.Length, destination.Length);
            for (int i = 0; i < length; i++)
            {
                char c = source[i];
                destination[i] = c <= 127 ? (byte)c : (byte)'?';
            }

            return length;
        }

        private static byte[] EncodeStaticAscii(string source)
        {
            byte[] bytes = new byte[source.Length]; // COLD ALLOC: byte[source.Length] — static CSV header bytes — owner: QAEnduranceCsvWriter
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                bytes[i] = c <= 127 ? (byte)c : (byte)'?';
            }

            return bytes;
        }
    }
}
