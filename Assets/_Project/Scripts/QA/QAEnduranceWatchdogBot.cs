using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
        IOriginShiftListener
    {
        private const string AgentId = "QA_WATCHDOG_BOT";
        private const string CsvFileName = "QA_Endurance_Log.csv";
        private const string DumpFileName = "Dump_QA_WATCHDOG_BOT.bin";
        private const string ResultFileName = "QAEnduranceResult_QA_WATCHDOG_BOT.json";
        private const string AutoRunFlagPath = "Temp/H8_QA_LEGACY_ENDURANCE.flag";
        private const string SaveSlotName = "qa_endurance_10km";
        private const int BlackBoxCapacity = 300;
        private const int CsvQueueCapacity = 64;
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
        private NativeArray<QAEnduranceBlackBoxEntry> _blackBox;
        private AbsoluteUniversePosition _lastAup;
        private AbsoluteUniversePosition _currentAup;
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
        private int _fpsSampleCount;
        private int _blackBoxCursor;
        private int _blackBoxCount;
        private int _nativeBlackBoxSentinelId;
        private int _originShiftCount;
        private int _trapCount;
        private int _saveRequestCount;
        private int _csvDropCount;
        private int _lastFrame;
        private bool _hasLastAup;
        private bool _automationInputPublished;
        private bool _tickRegistered;
        private bool _originListenerRegistered;
        private bool _instanceAccepted;
        private bool _active;
        private bool _pdaOpen;
        private bool _saveInFlight;
        private bool _completed;
        private bool _faulted;
        private string _csvPath;
        private string _dumpPath;
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

                if (Enum.TryParse(args[i + 1], true, out QAEnduranceTier parsed))
                    return parsed;
            }

            return QAEnduranceTier.Low;
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
            _blackBox = new NativeArray<QAEnduranceBlackBoxEntry>( // COLD ALLOC: NativeArray[300] — QA crash blackbox ring — owner: QAEnduranceWatchdogBot
                BlackBoxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _nativeBlackBoxSentinelId = NativeMemorySentinel.RegisterNativeArray(
                _blackBox,
                nameof(QAEnduranceWatchdogBot),
                nameof(_blackBox),
                NativeAllocationLifetime.Session);
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

            if (_blackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_blackBox);
                _blackBox.Dispose();
            }

            _nativeBlackBoxSentinelId = 0;
        }

        public void BeginRun()
        {
            if (_active)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));
            _csvWriter = new QAEnduranceCsvWriter(_csvPath, CsvQueueCapacity, CsvLineCapacity, CsvByteCapacity); // COLD ALLOC: QAEnduranceCsvWriter[1] — decoupled CSV writer — owner: QAEnduranceWatchdogBot
            _csvWriter.Start();
            _memoryWindowStartBytes = Profiler.GetTotalAllocatedMemoryLong();
            _lastTotalMemoryBytes = _memoryWindowStartBytes;
            _lastManagedMemoryBytes = Profiler.GetMonoUsedSizeLong();
            _lastGraphicsDriverBytes = ResolveGraphicsDriverBytes();
            _nextCsvDistance = ResolveCsvIntervalMeters();
            _nextPdaDistance = pdaIntervalMeters;
            _nextSaveDistance = saveIntervalMeters;
            _nextMemoryWindowDistance = MemoryLeakWindowMeters;
            _distanceMeters = 0f;
            _fpsAccumulated = 0f;
            _fpsSampleCount = 0;
            _blackBoxCursor = 0;
            _blackBoxCount = 0;
            _originShiftCount = 0;
            _trapCount = 0;
            _saveRequestCount = 0;
            _csvDropCount = 0;
            _lastFrame = -1;
            _hasLastAup = false;
            _completed = false;
            _faulted = false;
            _active = true;

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

            if (Time.frameCount < MinimumFrameBeforeSampling || _lastFrame == Time.frameCount)
                return;

            _lastFrame = Time.frameCount;
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
                TogglePdaRadar(in _currentAup);

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
            switch (tier)
            {
                case QAEnduranceTier.Ultra:
                    return math.min(csvIntervalMeters, 250f);
                case QAEnduranceTier.High:
                    return math.min(csvIntervalMeters, 500f);
                default:
                    return csvIntervalMeters;
            }
        }

        private void ResolveArtifactPaths()
        {
            string logRoot = ResolveProjectPath("Docs/AgentLogs");
            _csvPath = Path.Combine(logRoot, CsvFileName);
            _dumpPath = Path.Combine(logRoot, DumpFileName);
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
            if (!_tickRegistered)
                _tickRegistered = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
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
            state.MoveDelta = new Vector2(0f, 1f);
            state.LookDelta = new Vector2(0f, -0.012f);
            state.VerticalDelta = 0.15f;
            state.ActionsBitmask = (uint)PlayerInputAction.Sprint;
            PhysicsDeterminismSignals.PublishInputOverride(in state, (uint)Time.frameCount);
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

                body.position = nextPosition;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
                if (runtimeContext.PlayerTransform != null)
                    runtimeContext.PlayerTransform.position = nextPosition;
                return;
            }

            Transform playerTransform = runtimeContext.PlayerTransform;
            if (playerTransform == null)
                return;

            Vector3 nextTransformPosition = playerTransform.position + offset;
            if (IsFinite(nextTransformPosition))
                playerTransform.position = nextTransformPosition;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private void TogglePdaRadar(in AbsoluteUniversePosition aup)
        {
            if (_pdaOpen)
                ThreadSafeCommandQueue.Enqueue(EntityCommand.CreateClosePDA());
            else
                ThreadSafeCommandQueue.Enqueue(EntityCommand.CreateOpenPDATab(pdaRadarTabIndex));

            _pdaOpen = !_pdaOpen;
            SonarPingSignal sonar = new SonarPingSignal
            {
                PositionAup = aup,
                RadiusMeters = ResolveRadarRadius(),
                Intensity01 = ResolveRadarIntensity(),
                SourceId = SourceHash,
                Flags = 1,
            };
            GlobalSignals.Publish(in sonar);
            _nextPdaDistance += pdaIntervalMeters;
            WriteBlackBox(EventHashPdaRadar);
            EnqueueCsvRecord(EventHashPdaRadar);
        }

        private float ResolveRadarRadius()
        {
            switch (tier)
            {
                case QAEnduranceTier.Ultra:
                    return 180f;
                case QAEnduranceTier.High:
                    return 140f;
                case QAEnduranceTier.Middle:
                    return 110f;
                default:
                    return 80f;
            }
        }

        private float ResolveRadarIntensity()
        {
            switch (tier)
            {
                case QAEnduranceTier.Ultra:
                    return 1f;
                case QAEnduranceTier.High:
                    return 0.85f;
                case QAEnduranceTier.Middle:
                    return 0.7f;
                default:
                    return 0.55f;
            }
        }

        private void RequestSaveIfAvailable()
        {
            _nextSaveDistance += saveIntervalMeters;
            if (_saveInFlight)
                return;

            ISaveService save = GlobalRegistry.Save;
            if (save == null || !save.IsInitialized || save.IsBusy)
                return;

            _saveInFlight = true;
            _saveRequestCount++;
            WriteBlackBox(EventHashSaveRequest);
            EnqueueCsvRecord(EventHashSaveRequest);
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
            WriteResultFile(0, EventHashComplete);
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
            DumpBlackBox();
            WriteResultFile(1, eventHash);
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
            PhysicsDeterminismSignals.ClearInputOverride();

            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _tickRegistered = false;
            }

            if (_originListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originListenerRegistered = false;
            }

            _active = false;

            if (_csvWriter != null)
            {
                _csvWriter.Dispose();
                _csvWriter = null;
            }
        }

        private void PublishCompliance(uint ruleHash, byte severity)
        {
            ComplianceViolationSignal signal = new ComplianceViolationSignal
            {
                RuleHash = ruleHash,
                SystemHash = SourceHash,
                ContextHash = AgentIdHash,
                Frame = (uint)Time.frameCount,
                Severity = severity,
                Flags = 1,
            };
            GlobalSignals.Publish(in signal);
        }

        private static uint AgentIdHash => 0x51415742u;

        private void WriteBlackBox(uint eventHash)
        {
            if (!_blackBox.IsCreated)
                return;

            QAEnduranceBlackBoxEntry entry = new QAEnduranceBlackBoxEntry
            {
                Frame = Time.frameCount,
                DistanceMeters = _distanceMeters,
                RuntimePosition = _currentRuntimePosition,
                Velocity = _currentVelocity,
                Aup = _currentAup,
                TotalMemoryBytes = _lastTotalMemoryBytes,
                ManagedMemoryBytes = _lastManagedMemoryBytes,
                GraphicsDriverBytes = _lastGraphicsDriverBytes,
                AverageFps = ResolveAverageFps(),
                EventHash = eventHash,
                Flags = BuildBlackBoxFlags(),
            };

            _blackBox[_blackBoxCursor] = entry;
            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;
            if (_blackBoxCount < BlackBoxCapacity)
                _blackBoxCount++;
        }

        private uint BuildBlackBoxFlags()
        {
            uint flags = 0u;
            if (_automationInputPublished)
                flags |= 1u;
            if (_pdaOpen)
                flags |= 1u << 1;
            if (_saveInFlight)
                flags |= 1u << 2;
            if (_faulted)
                flags |= 1u << 3;
            return flags;
        }

        private void EnqueueCsvRecord(uint eventHash)
        {
            if (_csvWriter == null)
                return;

            QAEnduranceCsvRecord record = new QAEnduranceCsvRecord
            {
                Frame = Time.frameCount,
                DistanceMeters = _distanceMeters,
                AverageFps = ResolveAverageFps(),
                TotalMemoryBytes = _lastTotalMemoryBytes,
                ManagedMemoryBytes = _lastManagedMemoryBytes,
                GraphicsDriverBytes = _lastGraphicsDriverBytes,
                RuntimeX = _currentRuntimePosition.x,
                RuntimeY = _currentRuntimePosition.y,
                RuntimeZ = _currentRuntimePosition.z,
                VelocityMagnitude = math.length(_currentVelocity),
                OriginShiftCount = _originShiftCount,
                TrapCount = _trapCount,
                SaveRequestCount = _saveRequestCount,
                CsvDropCount = _csvDropCount,
                Tier = (byte)tier,
                EventHash = eventHash,
            };

            if (!_csvWriter.TryEnqueue(in record))
                _csvDropCount++;
        }

        private void DumpBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));
            using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read)) // COLD ALLOC: FileStream[1] — crash blackbox dump — owner: QAEnduranceWatchdogBot
            using (BinaryWriter writer = new BinaryWriter(stream)) // COLD ALLOC: BinaryWriter[1] — crash blackbox binary encoder — owner: QAEnduranceWatchdogBot
            {
                writer.Write(0x51415744);
                writer.Write(1);
                writer.Write(_blackBoxCount);
                writer.Write(_blackBoxCursor);
                for (int i = 0; i < _blackBoxCount; i++)
                {
                    int index = _blackBoxCursor - _blackBoxCount + i;
                    if (index < 0)
                        index += BlackBoxCapacity;

                    QAEnduranceBlackBoxEntry entry = _blackBox[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.DistanceMeters);
                    writer.Write(entry.RuntimePosition.x);
                    writer.Write(entry.RuntimePosition.y);
                    writer.Write(entry.RuntimePosition.z);
                    writer.Write(entry.Velocity.x);
                    writer.Write(entry.Velocity.y);
                    writer.Write(entry.Velocity.z);
                    writer.Write(entry.Aup.GridX);
                    writer.Write(entry.Aup.GridY);
                    writer.Write(entry.Aup.GridZ);
                    writer.Write(entry.Aup.LocalX);
                    writer.Write(entry.Aup.LocalY);
                    writer.Write(entry.Aup.LocalZ);
                    writer.Write(entry.TotalMemoryBytes);
                    writer.Write(entry.ManagedMemoryBytes);
                    writer.Write(entry.GraphicsDriverBytes);
                    writer.Write(entry.AverageFps);
                    writer.Write(entry.EventHash);
                    writer.Write(entry.Flags);
                }
            }
        }

        private void WriteResultFile(int exitCode, uint eventHash)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
            using (StreamWriter writer = new StreamWriter(_resultPath, false)) // COLD ALLOC: StreamWriter[1] — terminal result JSON — owner: QAEnduranceWatchdogBot
            {
                writer.Write("{\"agent\":\"");
                writer.Write(AgentId);
                writer.Write("\",\"exitCode\":");
                writer.Write(exitCode.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"eventHash\":");
                writer.Write(eventHash.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"distanceMeters\":");
                writer.Write(_distanceMeters.ToString("F3", CultureInfo.InvariantCulture));
                writer.Write(",\"originShifts\":");
                writer.Write(_originShiftCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"traps\":");
                writer.Write(_trapCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"saveRequests\":");
                writer.Write(_saveRequestCount.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"csvDrops\":");
                writer.Write(_csvDropCount.ToString(CultureInfo.InvariantCulture));
                writer.Write("}");
            }
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
        [FieldOffset(73)] private byte _pad1;
        [FieldOffset(74)] private ushort _pad2;
        [FieldOffset(76)] public uint EventHash;
    }

    internal sealed class QAEnduranceCsvWriter : IDisposable
    {
        private static readonly char[] FloatFormat = { 'F', '3' }; // COLD ALLOC: char[2] — fixed float format token — owner: QAEnduranceCsvWriter
        private static readonly byte[] HeaderBytes = EncodeStaticAscii(
            "frame,distanceMeters,avgFps,totalMemoryBytes,managedMemoryBytes,graphicsDriverBytes,x,y,z,velocityMetersPerSecond,originShifts,traps,saveRequests,csvDrops,tier,eventToken,eventHash\n"); // COLD ALLOC: byte[headerLength] — static CSV header bytes — owner: QAEnduranceCsvWriter

        private readonly QAEnduranceCsvRecord[] _records;
        private readonly object _gate = new object(); // COLD ALLOC: object[1] — CSV queue lock gate — owner: QAEnduranceCsvWriter
        private readonly AutoResetEvent _signal = new AutoResetEvent(false); // COLD ALLOC: AutoResetEvent[1] — CSV writer wake signal — owner: QAEnduranceCsvWriter
        private readonly char[] _lineChars;
        private readonly byte[] _lineBytes;
        private readonly string _path;
        private Thread _thread;
        private FileStream _stream;
        private int _readIndex;
        private int _writeIndex;
        private int _count;
        private volatile bool _running;

        public QAEnduranceCsvWriter(string path, int capacity, int lineCapacity, int byteCapacity)
        {
            _path = path;
            _records = new QAEnduranceCsvRecord[math.max(4, capacity)]; // COLD ALLOC: QAEnduranceCsvRecord[capacity] — bounded CSV record ring — owner: QAEnduranceCsvWriter
            _lineChars = new char[math.max(128, lineCapacity)]; // COLD ALLOC: char[lineCapacity] — CSV line format buffer — owner: QAEnduranceCsvWriter
            _lineBytes = new byte[math.max(256, byteCapacity)]; // COLD ALLOC: byte[byteCapacity] — CSV ASCII write buffer — owner: QAEnduranceCsvWriter
        }

        public void Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            _stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous); // COLD ALLOC: FileStream[1] — async CSV file sink — owner: QAEnduranceCsvWriter
            _stream.Write(HeaderBytes, 0, HeaderBytes.Length);
            _running = true;
            _thread = new Thread(WriterLoop) // COLD ALLOC: Thread[1] — background CSV writer — owner: QAEnduranceCsvWriter
            {
                IsBackground = true,
                Name = "H8.QA.EnduranceCsvWriter",
            };
            _thread.Start();
        }

        public bool TryEnqueue(in QAEnduranceCsvRecord record)
        {
            lock (_gate)
            {
                if (_count >= _records.Length)
                    return false;

                _records[_writeIndex] = record;
                _writeIndex++;
                if (_writeIndex >= _records.Length)
                    _writeIndex = 0;
                _count++;
            }

            _signal.Set();
            return true;
        }

        public void Dispose()
        {
            _running = false;
            _signal.Set();
            if (_thread != null)
            {
                _thread.Join(2000);
                _thread = null;
            }

            DrainPending();
            if (_stream != null)
            {
                _stream.Flush();
                _stream.Dispose();
                _stream = null;
            }

            _signal.Dispose();
        }

        private void WriterLoop()
        {
            while (_running)
            {
                DrainPending();
                _signal.WaitOne(100);
            }

            DrainPending();
        }

        private void DrainPending()
        {
            while (TryDequeue(out QAEnduranceCsvRecord record))
                WriteRecord(in record);
        }

        private bool TryDequeue(out QAEnduranceCsvRecord record)
        {
            lock (_gate)
            {
                if (_count <= 0)
                {
                    record = default;
                    return false;
                }

                record = _records[_readIndex];
                _records[_readIndex] = default;
                _readIndex++;
                if (_readIndex >= _records.Length)
                    _readIndex = 0;
                _count--;
                return true;
            }
        }

        private void WriteRecord(in QAEnduranceCsvRecord record)
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
            AppendEventToken(chars, ref cursor, record.EventHash);
            AppendComma(chars, ref cursor);
            AppendUInt(chars, ref cursor, record.EventHash);
            AppendNewLine(chars, ref cursor);

            int byteCount = EncodeAscii(chars.Slice(0, cursor), _lineBytes);
            _stream.WriteAsync(_lineBytes, 0, byteCount).GetAwaiter().GetResult();
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
