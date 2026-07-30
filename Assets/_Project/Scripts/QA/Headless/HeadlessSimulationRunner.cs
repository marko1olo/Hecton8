using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.QA.Headless
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class HeadlessSimulationRunner : MonoBehaviour, IFastTickable, IFrostTickable, IColdTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HeadlessSimulationRunnerSignalPushDropCount;
        private const string RunnerName = "HEADLESS_SIMULATION_RUNNER";
        private const string RuntimeRootName = "[HeadlessSimulationRunner]";
        private const string CommandLineArg = "-h8headless";
        private const string LegacyCommandLineArg = "-headless";
        private const string DaysArg = "-h8headlessDays";
        private const string DaySecondsArg = "-h8headlessDaySeconds";
        private const string StartupTimeoutArg = "-h8headlessStartupTimeout";
        private const string EnvironmentFlagName = "H8_HEADLESS_SIMULATION";
        private const string FlagRelativePath = "Temp/H8_HEADLESS_SIMULATION.flag";
        private const string CsvRelativePath = "Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv";
        private const string ResultRelativePath = "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json";
        private const string H8MemoryDumpRelativePath = "Docs/AgentLogs/H8Memory_HEADLESS_SIMULATION_RUNNER.txt";
        private const string BlackboxRelativePath = "Docs/AgentLogs/Dump_HEADLESS_SIMULATION_RUNNER.bin";
        private const int BlackboxFrameCapacity = 300;
        private const int BlackboxEntrySizeBytes = 64;
        private const int MemoryWindowDays = 10;
        private const int MaxConsecutiveMemoryWindowFailures = 3;
        // Same shape and same number as the memory window above, for the same reason: the answer can be
        // "not right now" rather than "never". IEcosystemDirectorService.TryGetGlobalBiomassAudit returns
        // false for six distinct conditions (EcosystemDirector.cs:3406-3417 and the IsFinite tail at :3451)
        // and one of them is a transient job fence: HasPendingSimulationJob() (:3407) is true from the frame
        // the ecology schedules work until the frame's LateFrameTick completes it, because a Burst job owns
        // the biomass buffers and reading them would be the job-safety violation that guard exists to avoid.
        //
        // That fence is not rare, and the dispatcher makes it deterministic rather than unlucky. Inside ONE
        // SystemDispatcher.RunDispatcherUpdate (SystemDispatcher.cs:5106) RunSlowTick (:5265) runs BEFORE
        // RunFrostTick (:5267); the ecology's SlowTick calls ScheduleSectorSolve (EcosystemDirector.cs:2729),
        // which sets _solveScheduled = true; _solveScheduled is cleared only in CompleteScheduledSolve,
        // reached from the ecology's LateFrameTick (:2792), which the dispatcher runs in a LATER player-loop
        // phase (RunDispatcherLateFrame, SystemDispatcher.cs:5453). So on any frame where a slow tick
        // scheduled the solve, every FrostTick that follows it in that same frame - including this runner's,
        // which is where the day boundary is evaluated - is guaranteed to be told "unavailable".
        //
        // The two _solveScheduled sites are named rather than numbered on purpose: they moved 53 lines during
        // the writing of this comment (5405 -> 5458 for the set, 5731 -> 5784 for the clear) because
        // EcosystemDirector.cs is under concurrent edit. Search the symbol, not the line.
        //
        // Treating that identically to a dead ecology meant one day boundary landing inside a job fence
        // aborted the whole run and blamed the ecology. Tolerance is bounded, never unbounded: three
        // CONSECUTIVE unsampled days still fail, and FinishRunIfTargetReached refuses to report SUCCESS for a
        // run that never sampled once.
        //
        // FIX 2026-07-30: day-boundary biomass sampling no longer runs in FrostTick. FrostTick only
        // accumulates completed-day debt (_pendingDayAudits). The sample itself runs in LateFrameTick,
        // AFTER ecology's Environment-lane LateFrameTick has called CompleteScheduledSimulation and
        // cleared HasPendingSimulationJob. Runner LateFrame is registered at PriorityLayer.Player so
        // the lane order Core -> Environment -> Player -> UI guarantees ecology completes first.
        // Streak only advances on a true dead/empty ecology (audit false with no pending job), never
        // on the deterministic same-frame fence that used to make every day "unsampled".
        private const int MaxConsecutiveEcologySampleFailures = 3;
        // CSV Flags bit for "this day produced no biomass sample at all". Deliberately NOT bit 0: bit 0 is
        // the only bit EcosystemBiomassAuditSample.Flags can ever carry (EcosystemDirector.cs:3422 seeds
        // flags = 0u, :3433 is the single `flags |= 1u`), and it means "a sampled cell was non-finite or
        // negative". The old code wrote 1u for the unsampled row too, which was harmless only while an
        // unsampled day killed the run immediately and could therefore only ever be the last row. Now that
        // unsampled days are tolerated and interleave with good ones, a reader has to be able to tell
        // "no sample" from "sampled, one bad cell" - so the marker gets its own bit.
        private const uint CsvFlagEcologySampleUnavailable = 1u << 8;
        private const int MaxSignalsDrainedPerFrame = 128;
        private const int MaxDailyAuditsPerFrostTick = 4;
        private const int DefaultTargetDays = 100;
        private const int AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
        private const float DefaultDaySeconds = 3600f;
        private const float DefaultStartupTimeoutSeconds = 180f;
        private const float TimeDilationScalar = 100f;
        private const float GhostSpeedMetersPerSecond = 85f;
        private const float NativeBytesToMegabytes = 1f / (1024f * 1024f);
        private const uint RunnerHash = 0x48385141u;
        private const uint SuccessHash = 0x48385130u;
        private const uint LeakHash = 0x48384C45u;
        private const uint EcologyCollapseHash = 0x48384543u;
        // 'H','8','N','S' - never sampled. Distinct from EcologyCollapseHash on purpose: the blackbox ring
        // stores only the reason hash, so a run that reached its target day count without ever obtaining a
        // biomass sample must not be indistinguishable from one whose predators actually died out.
        private const uint EcologyNeverSampledHash = 0x48384E53u;
        private const uint GasInvalidHash = 0x48384741u;
        private const uint NaNHash = 0x48384E41u;
        private const uint TimeoutHash = 0x4838544Fu;
        private const uint AupShiftHash = 0x48384155u;
        private const uint CsvWriteHash = 0x48384353u;
        private const uint DataVaultUnavailableHash = 0x48384456u;
        private const uint EvidenceBlackboxWriteFailed = 1u << 0;
        private const uint EvidenceResultWriteFailed = 1u << 1;
        private const uint EvidenceCsvWriteFailed = 1u << 2;
        private const SystemID OwnerSystemId = SystemID.QAHeadless;
        private const BufferID GhostStateBufferId = BufferID.HeadlessSimulationGhostState;
        private const BufferID BlackboxBufferId = BufferID.HeadlessSimulationBlackBox;
        private const BufferID MemoryWindowBytesBufferId = BufferID.HeadlessSimulationMemoryWindowBytes;
        private const BufferID MemoryWindowH8BytesBufferId = BufferID.HeadlessSimulationMemoryWindowH8Bytes;
        private const BufferID MemoryWindowAllocationCountsBufferId = BufferID.HeadlessSimulationMemoryWindowAllocationCounts;

        private static HeadlessSimulationRunner _instance;

        private VaultGenerationHandle<GhostState> _ghostStateHandle;
        private VaultGenerationHandle<HeadlessTelemetryEntry> _blackboxHandle;
        private VaultGenerationHandle<long> _memoryWindowBytesHandle;
        private VaultGenerationHandle<long> _memoryWindowH8BytesHandle;
        private VaultGenerationHandle<int> _memoryWindowAllocationCountsHandle;
        private IDataVault _dataVault;
        private HeadlessCsvWriter _csvWriter;
        private string _resultPath;
        private string _blackboxPath;
        private double _ghostSeconds;
        private double _simulatedSeconds;
        private double _dayAccumulatorSeconds;
        private double _startupTime;
        // Wall clock for ecology-ready budget. Armed only after lanes are live AND
        // BootstrapState.IsGameReady (or bootstrap presence cleared). Measuring from BeginStartup
        // falsely burned the entire 180s budget during dependency init before GameReady opened
        // dispatcher FrostTick — p0_gameready 2026-07-30 BOOTSTRAP_TIMEOUT at short-circuit.
        private double _ecologyWaitStartRealtime;
        private int _ecologyWaitDiagBucket = -1;
        private float _daySeconds = DefaultDaySeconds;
        private float _startupTimeoutSeconds = DefaultStartupTimeoutSeconds;
        private int _targetDays = DefaultTargetDays;
        private int _completedDays;
        private int _memoryWindowCursor;
        private int _memoryWindowCount;
        private int _memoryWindowFailureStreak;
        private int _ecologySampleFailureStreak;
        private int _ecologySampledDayCount;
        private int _ecologyUnsampledDayCount;
        // Day boundaries detected in FrostTick; biomass sample deferred to LateFrameTick (post job fence).
        private int _pendingDayAudits;
        private int _blackboxCursor;
        private int _progressionSignalCount;
        private int _crashSignalCount;
        private int _gasInvalidRoomId = -1;
        private int _debugLogDeliveredCount;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private int _previousCaptureFramerate;
        private long _lastMemoryBytes;
        private long _lastH8MemoryBytes;
        private float _lastPreyBiomass;
        private float _lastPredatorBiomass;
        private GhostState _pendingGhostState;
        private LogType _previousLogFilter;
        private uint _lastProgressionHash;
        private uint _lastCrashReasonHash;
        private uint _lastSyntheticShiftSequence;
        private uint _actualOriginShiftCount;
        private uint _evidenceFailureFlags;
        private bool _previousRunInBackground;
        private bool _started;
        private bool _registeredFast;
        private bool _registeredFrost;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _originListenerRegistered;
        private bool _ghostStepPending;
        private bool _ghostStateInitialized;
        private bool _ecologyReady;
        private double _simulationStartRealtime;
        private bool _finished;
        private bool _runtimePolicyCaptured;
        private bool _awaitingDispatcher;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null || !ShouldRunStatic())
                return;

            GameObject root = new GameObject(RuntimeRootName);
            _instance = root.AddComponent<HeadlessSimulationRunner>();
            DontDestroyOnLoad(root);
        }

        private void Start()
        {
            if (!ShouldRunStatic())
            {
                Destroy(gameObject);
                return;
            }

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LogRunnerLifecycle("runner installed and started");
            BeginStartup();
        }

        private void BeginStartup()
        {
            try
            {
                InitializeColdState();
                // Do NOT await NextFrameAsync for the dispatcher gate. In -batchmode
                // AwaitableDebtMonitor.NextFrameAsync is Task.Yield + MainThreadAsync; the
                // runner continuation is not guaranteed to resume while bootstrap holds the
                // main await chain. Evidence: headless_smoke_20260730_p0fix.log logs
                // "waiting for dispatcher" then SystemDispatcher init + SceneActivate
                // short-circuit, then silence until BATCH_TIMEOUT — no DISPATCHER_TIMEOUT
                // either, because ColdTick only runs after RegisterRuntimeLanes.
                // Player-loop Update always runs in editor playmode; poll there instead.
                _startupTime = Time.realtimeSinceStartupAsDouble;
                _awaitingDispatcher = true;
                LogRunnerLifecycle("waiting for dispatcher");
                TryCompleteDispatcherWait();
            }
            catch (IOException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (UnauthorizedAccessException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (InvalidOperationException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (ArgumentException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }

        private void Update()
        {
            if (_finished)
                return;

            // Wall-clock ecology timeout must NOT depend on ColdTick (ticks can starve).
            // CRITICAL: budget starts at GameReady/bootstrap-exit, NOT BeginStartup.
            // p0_gameready (2026-07-30): _startupTime armed at runner install; bootstrap
            // dependency chain burned ~180s; short-circuit PublishGameReady then immediate
            // BOOTSTRAP_TIMEOUT with zero post-GameReady FrostTick budget.
            if (_started && !_ecologyReady)
            {
                TryArmEcologyWaitClock();
                // Ready-mark is a gate, not a sim-tick substitute. FrostTick can starve while
                // FO bootstrap lock / frame lock / dilation=0 hold RunDispatcherUpdate off the
                // master sim path; ecoInit was true from t=0 in p0_fo_lock_drain while
                // TryMarkEcologyReady never ran (Frost-only). Same starvation-proof pattern as
                // moving the wait clock off ColdTick onto Update.
                TryMarkEcologyReady();
                if (_ecologyReady)
                    return;
                // Keep FO scene-rebase barrier draining while we wait — dispatcher early-returns
                // all Frost/LateFrame while IsOriginShiftBootstrapLocked holds.
                HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
                MaybeLogEcologyWaitProgress();
                if (_ecologyWaitStartRealtime > 0.0 &&
                    Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime > _startupTimeoutSeconds)
                {
                    LogEcologyBootstrapTimeoutDiagnostics();
                    FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                    return;
                }
            }

            if (!_awaitingDispatcher)
                return;

            TryCompleteDispatcherWait();
        }

        private void OnDestroy()
        {
            _awaitingDispatcher = false;
            _ghostStepPending = false;
            TryUnregisterHotSwapListener();
            UnregisterRuntimeLanes();
            if (_originListenerRegistered)
                HectonFloatingOrigin.UnregisterListener(this);
            Application.logMessageReceived -= HandleLogMessage;
            RestoreRuntimePolicy();

            ReleaseVaultBuffers();
            _csvWriter?.Dispose();
            _csvWriter = null;
            if (_instance == this)
                _instance = null;
        }

        public void FastTick(float deltaTime)
        {
            if (!_started || _finished)
                return;

            DrainSignals();
            float safeDelta = math.isfinite(deltaTime) && deltaTime > 0f ? math.min(deltaTime, 120f) : 0f;
            if (safeDelta > 0f)
                _ghostSeconds += safeDelta;

            if (safeDelta > 0f && _ecologyReady)
            {
                _simulatedSeconds += safeDelta;
                _dayAccumulatorSeconds += safeDelta;
            }

            if (!_ghostStepPending)
            {
                if (!TryReadGhostState(out NativeArray<GhostState>.ReadOnly ghostState) ||
                    !ghostState.IsCreated ||
                    ghostState.Length <= 0)
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[GHOST_BUFFER_UNAVAILABLE]");
                    return;
                }

                GhostState current = ghostState[0];
                _pendingGhostState = ResolveNextGhostState(
                    in current,
                    safeDelta,
                    _ghostSeconds,
                    GhostSpeedMetersPerSecond);
                _ghostStepPending = true;
            }

            RecordBlackbox(0u);
        }

        public void LateFrameTick()
        {
            if (!_started || _finished)
                return;

            // Ghost commit first (existing path). Day audits run after so a failed ghost write still
            // aborts before we charge ecology for a day the harness could not advance.
            if (_ghostStepPending)
            {
                _ghostStepPending = false;
                if (!TryCommitPendingGhostState(out GhostState previous, out GhostState next))
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[GHOST_BUFFER_WRITE_FAILED]");
                    return;
                }

                HandleSyntheticAupShift(in previous, in next);
                if (!math.all(math.isfinite(next.AbsoluteMeters)) ||
                    !math.isfinite(next.RuntimeMeters.x) ||
                    !math.isfinite(next.RuntimeMeters.y) ||
                    !math.isfinite(next.RuntimeMeters.z))
                {
                    FailAndQuit(1, NaNHash, "[NAN_DETECTED]");
                    return;
                }
            }

            // Biomass sample after ecology LateFrame (Environment lane) completed scheduled jobs.
            // See MaxConsecutiveEcologySampleFailures comment block for the fence chronology.
            if (_ecologyReady && _pendingDayAudits > 0)
                DrainPendingDayAudits();
        }

        public void FrostTick()
        {
            if (!_started || _finished)
                return;

            TryArmEcologyWaitClock();
            TryMarkEcologyReady();
            if (!_ecologyReady)
                return;

            if (!AuditGasPressureFinite())
            {
                FailAndQuit(1, GasInvalidHash, "[GAS_INVALID]");
                return;
            }

            // Do NOT sample biomass here. SlowTick (ecology) schedules the sector solve earlier in this
            // same dispatcher update, and the job fence stays up until ecology's LateFrameTick. Queue
            // day debt only; LateFrameTick drains it after the fence clears.
            int auditsThisTick = 0;
            int remainingDays = _targetDays - _completedDays - _pendingDayAudits;
            while (_dayAccumulatorSeconds >= _daySeconds &&
                   remainingDays > 0 &&
                   auditsThisTick < MaxDailyAuditsPerFrostTick &&
                   !_finished)
            {
                _dayAccumulatorSeconds -= _daySeconds;
                _pendingDayAudits++;
                remainingDays--;
                auditsThisTick++;
            }
        }

        public void ColdTick()
        {
            if (_finished)
                return;

            if (!_started)
                return;

            if (!_ecologyReady)
            {
                TryArmEcologyWaitClock();
                if (_ecologyWaitStartRealtime > 0.0 &&
                    Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime > _startupTimeoutSeconds)
                {
                    LogEcologyBootstrapTimeoutDiagnostics();
                    FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                }
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _actualOriginShiftCount++;
        }

        private void TryCompleteDispatcherWait()
        {
            if (!_awaitingDispatcher || _finished)
                return;

            if (GlobalRegistry.Dispatcher == null)
            {
                if (Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)
                {
                    _awaitingDispatcher = false;
                    FailAndQuit(1, TimeoutHash, "[DISPATCHER_TIMEOUT]");
                }

                return;
            }

            _awaitingDispatcher = false;
            LogRunnerLifecycle("dispatcher acquired");

            try
            {
                ForceHeadlessRuntimePolicy();
                CacheDataVaultCold();
                if (!EnsureVaultBuffersCold() || !TryInitializeGhostState())
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[DATAVAULT_UNAVAILABLE]");
                    return;
                }

                RegisterRuntimeLanes();
                TryRegisterHotSwapListener();
                HectonFloatingOrigin.RegisterListener(this);
                _originListenerRegistered = true;
                GlobalRegistry.TickDispatcher?.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
                if (!_started)
                    FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
                else
                    LogRunnerLifecycle("runtime lanes registered; dilation requested");
            }
            catch (IOException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (UnauthorizedAccessException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (InvalidOperationException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (ArgumentException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }

        private void RegisterRuntimeLanes()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_registeredFast || _registeredFrost || _registeredCold || _registeredLate)
                UnregisterRuntimeLanes();

            _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Core);
            _registeredFrost = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Core);
            _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core);
            // Player lane runs after Environment: ecology LateFrame completes jobs before this runner samples.
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            _started = _registeredFast && _registeredFrost && _registeredCold && _registeredLate;
            if (!_started)
                UnregisterRuntimeLanes();
        }

        private void UnregisterRuntimeLanes()
        {
            if (_registeredFast)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Core);
                _registeredFast = false;
            }

            if (_registeredFrost)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Core);
                _registeredFrost = false;
            }

            if (_registeredCold)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Core);
                _registeredCold = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLate = false;
            }

            _started = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (_started || _finished)
                    return;

                _dataVault = currentService as IDataVault;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            UnregisterRuntimeLanes();
            if (currentService == null)
            {
                return;
            }

            if (_finished || !isActiveAndEnabled)
                return;

            RegisterRuntimeLanes();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void InitializeColdState()
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            _targetDays = math.max(1, TryReadInt(args, DaysArg, DefaultTargetDays));
            _daySeconds = math.max(1f, TryReadFloat(args, DaySecondsArg, DefaultDaySeconds));
            _startupTimeoutSeconds = math.max(1f, TryReadFloat(args, StartupTimeoutArg, DefaultStartupTimeoutSeconds));
            _resultPath = ResolveProjectPath(ResultRelativePath);
            _blackboxPath = ResolveProjectPath(BlackboxRelativePath);
            string csvPath = ResolveProjectPath(CsvRelativePath);
            EnsureParentDirectory(_resultPath);
            EnsureParentDirectory(_blackboxPath);
            EnsureParentDirectory(csvPath);
            EnsureVaultBuffersCold();
            TryInitializeGhostState();
            _csvWriter = new HeadlessCsvWriter(csvPath);
            _csvWriter.WriteHeader();
            Application.logMessageReceived += HandleLogMessage;
        }

        private void ForceHeadlessRuntimePolicy()
        {
            if (!_runtimePolicyCaptured)
            {
                _previousRunInBackground = Application.runInBackground;
                _previousTargetFrameRate = Application.targetFrameRate;
                _previousVSyncCount = QualitySettings.vSyncCount;
                _previousCaptureFramerate = Time.captureFramerate;
                _previousLogFilter = Debug.unityLogger.filterLogType;
                _runtimePolicyCaptured = true;
            }

            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Time.captureFramerate = 0;
            // The log filter is DELIBERATELY not set here any more. It is installed at the first
            // ecology-ready transition instead - see TryMarkEcologyReady. This method runs the instant the
            // dispatcher wait succeeds, which is MID-BOOT, and muzzling Debug.Log there blinded the entire
            // remaining bootstrap: on 2026-07-29 the last surviving managed line in a 27,107-line log was a
            // [GameBootstrapper] node, and two separate investigations read that silence as a bootstrap
            // deadlock and diagnosed the wrong subsystem. _previousLogFilter is still captured above so
            // RestoreRuntimePolicy stays symmetric whichever path set it.
            GlobalRegistry.RegisterScalabilityTierOverride(1);
            GlobalRegistry.RegisterMathPrecisionLevel(MathPrecisionLevel.High);
            DistanceMath.PushShaderMathLod(1f);
        }

        private void RestoreRuntimePolicy()
        {
            if (!_runtimePolicyCaptured)
                return;

            Application.runInBackground = _previousRunInBackground;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
            Time.captureFramerate = _previousCaptureFramerate;
            Debug.unityLogger.filterLogType = _previousLogFilter;
            _runtimePolicyCaptured = false;
        }


        /// <summary>
        /// Arms the ecology-ready wall clock once bootstrap has opened gameplay ticks.
        /// Uses IsGameReady so headless short-circuit and full ActivatePlayer paths both qualify.
        /// Does not arm during dependency init (p0_gameready burned 180s pre-GameReady).
        /// </summary>
        private void TryArmEcologyWaitClock()
        {
            if (_ecologyWaitStartRealtime > 0.0)
                return;

            // GameReady is the hard signal. HasActiveInstance==false alone is insufficient
            // during early boot before PublishBootstrapPresence(true).
            if (!BootstrapState.IsGameReady)
                return;

            _ecologyWaitStartRealtime = Time.realtimeSinceStartupAsDouble;
            LogRunnerLifecycle("ecology wait clock armed (GameReady)");
        }

        private void LogEcologyBootstrapTimeoutDiagnostics()
        {
            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            bool ecoNull = ecosystem == null;
            bool ecoInit = !ecoNull && ecosystem.IsInitialized;
            bool foFlushClean = HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
            HectonFloatingOrigin.CopyBootstrapDrainSnapshot(
                out bool foHasOrigin,
                out bool foShift,
                out bool foPhysicsPause,
                out bool foLock,
                out int foPendingScenes,
                out bool foTargetsDirty,
                out bool foBarrier);
            bool dispBootstrapLocked = SystemDispatcher.IsOriginShiftBootstrapLocked;
            // FailAndQuit muzzles Log after ecologyReady; we are pre-ready so Log is fine.
            LogRunnerLifecycle(
                "BOOTSTRAP_TIMEOUT diag ecoNull=" + (ecoNull ? "1" : "0") +
                " ecoInit=" + (ecoInit ? "1" : "0") +
                " foFlushClean=" + (foFlushClean ? "1" : "0") +
                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +
                " foShift=" + (foShift ? "1" : "0") +
                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +
                " foLock=" + (foLock ? "1" : "0") +
                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +
                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +
                " foBarrier=" + (foBarrier ? "1" : "0") +
                " dispBootstrapLocked=" + (dispBootstrapLocked ? "1" : "0") +
                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0") +
                " hasBootstrap=" + (BootstrapState.HasActiveInstance ? "1" : "0"));
        }

        /// <summary>
        /// Periodic pre-ecology wait trace so external BATCH_TIMEOUT still leaves FO lock state on disk.
        /// </summary>
        private void MaybeLogEcologyWaitProgress()
        {
            if (_ecologyWaitStartRealtime <= 0.0)
                return;

            double waited = Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime;
            int bucket = (int)(waited / 15.0);
            if (bucket <= _ecologyWaitDiagBucket)
                return;

            _ecologyWaitDiagBucket = bucket;
            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            bool ecoNull = ecosystem == null;
            bool ecoInit = !ecoNull && ecosystem.IsInitialized;
            HectonFloatingOrigin.CopyBootstrapDrainSnapshot(
                out bool foHasOrigin,
                out bool foShift,
                out bool foPhysicsPause,
                out bool foLock,
                out int foPendingScenes,
                out bool foTargetsDirty,
                out bool foBarrier);
            LogRunnerLifecycle(
                "ecology wait progress t=" + waited.ToString("0.0", CultureInfo.InvariantCulture) +
                "s ecoNull=" + (ecoNull ? "1" : "0") +
                " ecoInit=" + (ecoInit ? "1" : "0") +
                " frostReg=" + (_registeredFrost ? "1" : "0") +
                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +
                " foShift=" + (foShift ? "1" : "0") +
                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +
                " foLock=" + (foLock ? "1" : "0") +
                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +
                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +
                " foBarrier=" + (foBarrier ? "1" : "0") +
                " dispBootstrapLocked=" + (SystemDispatcher.IsOriginShiftBootstrapLocked ? "1" : "0") +
                " dispFrameLocked=" + (SystemDispatcher.IsOriginShiftFrameLockedForCurrentFrame ? "1" : "0"));
        }

        private void TryMarkEcologyReady()
        {
            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            bool readyNow = ecosystem != null && ecosystem.IsInitialized;

            // Stamp the wall clock the first time simulation actually starts advancing, so the result file can
            // report the dilation it DELIVERED rather than the one it was configured with. Those two numbers
            // are far apart and the gap is the single most useful thing this harness can tell its operator —
            // see the deliveredTimeDilation note in WriteResult.
            if (readyNow && !_ecologyReady)
            {
                _simulationStartRealtime = Time.realtimeSinceStartupAsDouble;
                LogRunnerLifecycle("ecology ready (ecosystem initialized)");

                // Muzzle Debug.Log HERE, not in ForceHeadlessRuntimePolicy. The filter exists so a 100-day
                // run's log is not drowned in first-party per-frame spam, and every source of that spam is
                // in the simulation loop that begins at this exact transition. Boot, by contrast, logs a
                // BOUNDED number of lines once and they are the most useful diagnostic this project has:
                // the [GameBootstrapper] dependency-node trace. Installing the filter at dispatcher-ready
                // threw that away for nothing - the 2026-07-29 run's log goes managed-silent immediately
                // after node 8 of the CoreServices phase, and both investigations of the resulting
                // [ECOLOGY_UNAVAILABLE] verdict misread that silence as a stall and blamed the ecology,
                // which turned out to be fully initialised and registered the whole time.
                //
                // Lifecycle lines survive either way: LogRunnerLifecycle uses LogWarning precisely because
                // this filter once ate the harness's own verdict (`[HEADLESS] fail` appeared zero times in
                // 27,107 lines while the result JSON sat on disk).
                Debug.unityLogger.filterLogType = LogType.Warning;
            }

            _ecologyReady = readyNow;
        }

        /// <summary>
        /// Simulated seconds advanced per real second, measured rather than assumed. Zero until simulation
        /// starts advancing.
        /// </summary>
        private double DeliveredTimeDilation
        {
            get
            {
                if (_simulationStartRealtime <= 0.0)
                    return 0.0;

                double elapsed = Time.realtimeSinceStartupAsDouble - _simulationStartRealtime;
                return elapsed > 0.0 ? _simulatedSeconds / elapsed : 0.0;
            }
        }

        private void DrainSignals()
        {
            int drained = 0;
            while (drained < MaxSignalsDrainedPerFrame && SignalBus<ProgressionEventSignal>.TryConsumeFrame(out ProgressionEventSignal progression))
            {
                _progressionSignalCount++;
                _lastProgressionHash = progression.PoiHash != 0u ? progression.PoiHash : progression.QuestHash;
                drained++;
            }

            drained = 0;
            while (drained < MaxSignalsDrainedPerFrame && SignalBus<CrashTelemetrySignal>.TryConsumeFrame(out CrashTelemetrySignal crash))
            {
                _crashSignalCount++;
                _lastCrashReasonHash = crash.ReasonHash;
                drained++;
            }
        }

        /// <summary>
        /// Drains day-boundary audits deferred from FrostTick. Invoked only from LateFrameTick so the
        /// ecology job fence from the same frame's SlowTick has already been completed.
        /// </summary>
        private void DrainPendingDayAudits()
        {
            int auditsThisTick = 0;
            while (_pendingDayAudits > 0 &&
                   _completedDays < _targetDays &&
                   auditsThisTick < MaxDailyAuditsPerFrostTick &&
                   !_finished)
            {
                _pendingDayAudits--;
                ExecuteDailyAudit();
                auditsThisTick++;
            }
        }

        private void ExecuteDailyAudit()
        {
            _completedDays++;
            long nativeBytes = GlobalRegistry.NativeTrackedBytes;
            long h8Bytes = H8Memory.TotalBytes;
            int nativeAllocations = GlobalRegistry.NativeAllocationCount;
            int h8Allocations = H8Memory.ActiveAllocationCount;
            _lastMemoryBytes = nativeBytes;
            _lastH8MemoryBytes = h8Bytes;

            if (DetectTenDayMemoryGrowth(nativeBytes, h8Bytes, h8Allocations, out bool memoryWindowUnavailable))
            {
                // A leak verdict is worthless without the owner-level allocation table, so dump it before quitting.
                TryDumpH8MemoryTable();
                FailAndQuit(1, LeakHash, "[LEAK_DETECTED]");
                return;
            }

            // A vault write refusal means the memory window could not be sampled, NOT that memory leaked.
            // Transient refusals (compaction fence, mutation guard, generation bump) are tolerated for a
            // bounded number of consecutive days so one defrag tick cannot abort a 100-day run.
            if (memoryWindowUnavailable)
            {
                _memoryWindowFailureStreak++;
                if (_memoryWindowFailureStreak >= MaxConsecutiveMemoryWindowFailures)
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[MEMORY_WINDOW_UNAVAILABLE]");
                    return;
                }
            }
            else
            {
                _memoryWindowFailureStreak = 0;
            }

            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            if (ecosystem == null || !ecosystem.TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample biomass))
            {
                // The CSV row goes out FIRST and unconditionally, for every unsampled day, tolerated or not.
                // The day counter has already advanced so a skipped row would leave a hole in the
                // series and a reader could not tell a tolerated miss from a missing measurement.
                _ecologyUnsampledDayCount++;
                if (!TryWriteDailyCsv(default, nativeBytes, h8Bytes, nativeAllocations, h8Allocations, CsvFlagEcologySampleUnavailable))
                    return;

                // After the LateFrame move, a false audit is no longer the deterministic same-frame job
                // fence (that fence is down by the time we sample). Streak still bounds true empty
                // ecology: headless with zero seeded biomass cells still fails by day 3.
                _ecologySampleFailureStreak++;
                if (_ecologySampleFailureStreak >= MaxConsecutiveEcologySampleFailures)
                {
                    // Bounded, so the tolerance cannot hide a permanently empty biomass table:
                    // with -h8headless there is no player; if EnsurePlayerSectorRegistered never seeds,
                    // _activeBiomassCellCount stays 0 and EVERY day is unsampled - that run still dies
                    // here, on day 3, with the same verdict.
                    FailAndQuit(1, EcologyCollapseHash, "[ECOLOGY_UNAVAILABLE]");
                    return;
                }

                // Must still run on this path. The day loop stops once _completedDays reaches
                // _targetDays, so a tolerated unsampled FINAL day would otherwise never reach any terminal
                // state: no completion, no failure, and the batch runner's watchdog left to notice hours
                // later. This is the only exit for that case.
                FinishRunIfTargetReached();
                return;
            }

            _ecologySampleFailureStreak = 0;
            _ecologySampledDayCount++;
            _lastPreyBiomass = biomass.PreyBiomassSum;
            _lastPredatorBiomass = biomass.PredatorBiomassSum;
            if (!TryWriteDailyCsv(biomass, nativeBytes, h8Bytes, nativeAllocations, h8Allocations, biomass.Flags))
                return;

            if (biomass.PredatorBiomassSum <= 0f)
            {
                FailAndQuit(1, EcologyCollapseHash, "[ECOLOGY_COLLAPSE]");
                return;
            }

            FinishRunIfTargetReached();
        }

        /// <summary>
        /// The only path to SUCCESS in this runner. A run that reached its target day count without ever
        /// obtaining a single biomass sample fails instead.
        /// </summary>
        /// <remarks>
        /// Sampling now runs in LateFrameTick after the ecology job fence clears, so the 2026-07-29
        /// same-frame unavailability path should not fire. The sample-count gate remains: without it, a
        /// permanently empty biomass table (seed never ran) could still reach target days with zero real
        /// samples if streak tolerance alone were trusted. A harness that can report success without
        /// evidence is worse than one that over-reports failure, so the sample count gates the verdict,
        /// not the day count alone.
        /// </remarks>
        private void FinishRunIfTargetReached()
        {
            if (_completedDays < _targetDays)
                return;

            if (_ecologySampledDayCount <= 0)
            {
                FailAndQuit(1, EcologyNeverSampledHash, "[ECOLOGY_NEVER_SAMPLED]");
                return;
            }

            CompleteAndQuit();
        }

        private bool AuditGasPressureFinite()
        {
            IGasDynamicsSolver gas = GlobalRegistry.GasDynamics;
            if (gas == null || !gas.IsInitialized)
                return true;

            int count = math.max(0, gas.RoomCount);
            for (int i = 0; i < count; i++)
            {
                if (!gas.TryGetRoomSnapshot(i, out GasRoomSnapshot snapshot))
                {
                    _gasInvalidRoomId = i;
                    return false;
                }

                float pressure = snapshot.PressureKPa;
                if (!math.isfinite(pressure) || pressure < 0f)
                {
                    _gasInvalidRoomId = i;
                    return false;
                }
            }

            return true;
        }

        private bool DetectTenDayMemoryGrowth(
            long nativeBytes,
            long h8Bytes,
            int h8Allocations,
            out bool sampleUnavailable)
        {
            sampleUnavailable = false;
            if (!EnsureVaultBuffersCold())
            {
                sampleUnavailable = true;
                return false;
            }

            int slot = _memoryWindowCursor % MemoryWindowDays;
            int nextCursor = _memoryWindowCursor + 1;
            int nextCount = math.min(_memoryWindowCount + 1, MemoryWindowDays);

            if (!WriteMemoryWindowLongSample(
                    in _memoryWindowBytesHandle,
                    MemoryWindowBytesBufferId,
                    slot,
                    nextCursor,
                    nextCount,
                    nativeBytes,
                    out bool nativeGrowth))
            {
                sampleUnavailable = true;
                return false;
            }

            if (!WriteMemoryWindowLongSample(
                    in _memoryWindowH8BytesHandle,
                    MemoryWindowH8BytesBufferId,
                    slot,
                    nextCursor,
                    nextCount,
                    h8Bytes,
                    out bool h8Growth))
            {
                sampleUnavailable = true;
                return false;
            }

            if (!WriteMemoryWindowIntSample(
                    in _memoryWindowAllocationCountsHandle,
                    MemoryWindowAllocationCountsBufferId,
                    slot,
                    nextCursor,
                    nextCount,
                    h8Allocations,
                    out bool allocationGrowth))
            {
                sampleUnavailable = true;
                return false;
            }

            _memoryWindowCursor = nextCursor;
            _memoryWindowCount = nextCount;
            return nativeGrowth || h8Growth || allocationGrowth;
        }

        private bool WriteMemoryWindowLongSample(
            in VaultGenerationHandle<long> handle,
            BufferID expectedBufferId,
            int slot,
            int nextCursor,
            int nextCount,
            long sample,
            out bool hasStrictGrowth)
        {
            hasStrictGrowth = false;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out NativeArray<long> samples))
            {
                return false;
            }

            try
            {
                if (!samples.IsCreated || samples.Length < MemoryWindowDays)
                    return false;

                samples[slot] = sample;
                hasStrictGrowth = nextCount >= MemoryWindowDays && HasStrictMemoryGrowth(samples, nextCursor);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private bool WriteMemoryWindowIntSample(
            in VaultGenerationHandle<int> handle,
            BufferID expectedBufferId,
            int slot,
            int nextCursor,
            int nextCount,
            int sample,
            out bool hasStrictGrowth)
        {
            hasStrictGrowth = false;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out NativeArray<int> samples))
            {
                return false;
            }

            try
            {
                if (!samples.IsCreated || samples.Length < MemoryWindowDays)
                    return false;

                samples[slot] = sample;
                hasStrictGrowth = nextCount >= MemoryWindowDays && HasStrictAllocationGrowth(samples, nextCursor);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private static bool HasStrictMemoryGrowth(NativeArray<long> samples, int cursor)
        {
            long previousBytes = samples[cursor % MemoryWindowDays];
            for (int i = 1; i < MemoryWindowDays; i++)
            {
                int index = (cursor + i) % MemoryWindowDays;
                long currentBytes = samples[index];
                if (currentBytes <= previousBytes)
                    return false;

                previousBytes = currentBytes;
            }

            return true;
        }

        private static bool HasStrictAllocationGrowth(NativeArray<int> allocationSamples, int cursor)
        {
            int previousCount = allocationSamples[cursor % MemoryWindowDays];
            for (int i = 1; i < MemoryWindowDays; i++)
            {
                int index = (cursor + i) % MemoryWindowDays;
                int currentCount = allocationSamples[index];
                if (currentCount <= previousCount)
                    return false;

                previousCount = currentCount;
            }

            return true;
        }

        private bool TryWriteDailyCsv(
            EcosystemBiomassAuditSample biomass,
            long nativeBytes,
            long h8Bytes,
            int nativeAllocations,
            int h8Allocations,
            uint flags)
        {
            if (_csvWriter == null)
                return true;

            try
            {
                bool wrote = _csvWriter.WriteDay(
                    _completedDays,
                    biomass.PreyBiomassSum,
                    biomass.PredatorBiomassSum,
                    biomass.CarryingCapacitySum,
                    nativeBytes,
                    h8Bytes,
                    nativeAllocations,
                    h8Allocations,
                    flags);
                if (!wrote)
                {
                    _csvWriter.DiscardPendingRow();
                    _evidenceFailureFlags |= EvidenceCsvWriteFailed;
                    FailAndQuit(1, CsvWriteHash, "[CSV_WRITE_FAILED]");
                    return false;
                }

                return true;
            }
            catch (IOException)
            {
                _csvWriter.DiscardPendingRow();
                _evidenceFailureFlags |= EvidenceCsvWriteFailed;
                FailAndQuit(1, CsvWriteHash, "[CSV_WRITE_FAILED]");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                _csvWriter.DiscardPendingRow();
                _evidenceFailureFlags |= EvidenceCsvWriteFailed;
                FailAndQuit(1, CsvWriteHash, "[CSV_WRITE_FAILED]");
                return false;
            }
            catch (ObjectDisposedException)
            {
                _csvWriter.DiscardPendingRow();
                _evidenceFailureFlags |= EvidenceCsvWriteFailed;
                FailAndQuit(1, CsvWriteHash, "[CSV_WRITE_FAILED]");
                return false;
            }
            catch (NotSupportedException)
            {
                _csvWriter.DiscardPendingRow();
                _evidenceFailureFlags |= EvidenceCsvWriteFailed;
                FailAndQuit(1, CsvWriteHash, "[CSV_WRITE_FAILED]");
                return false;
            }
        }

        private void HandleSyntheticAupShift(in GhostState previous, in GhostState next)
        {
            int3 previousGrid = new int3(
                ClampGrid(previous.Aup.GridX),
                ClampGrid(previous.Aup.GridY),
                ClampGrid(previous.Aup.GridZ));
            int3 nextGrid = new int3(
                ClampGrid(next.Aup.GridX),
                ClampGrid(next.Aup.GridY),
                ClampGrid(next.Aup.GridZ));
            int3 delta = nextGrid - previousGrid;
            if (math.all(delta == int3.zero))
                return;

            _lastSyntheticShiftSequence++;
            float3 shiftMeters = new float3(delta.x, delta.y, delta.z) * AupCellSizeMeters;
            uint sequence = _lastSyntheticShiftSequence == 0u ? 1u : _lastSyntheticShiftSequence;
            AupSignalRoute.TryQueuePreShift(new AupPreShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            SignalBus<RebaseSignal>.TryPushTracked(new RebaseSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                GridDelta = delta,
                Flags = 1u
            }, ref s_x001HeadlessSimulationRunnerSignalPushDropCount);
            AupSignalRoute.TryQueueShift(new AupShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            RecordBlackbox(AupShiftHash);
        }

        /// <summary>
        /// Writes the run report FIRST, then the optional telemetry, then quits.
        /// </summary>
        /// <remarks>
        /// The ordering is the whole point and it is not stylistic. This method used to write the result
        /// file LAST, after RecordBlackbox, PublishCrashSignal and TryDumpBlackbox - three calls into the
        /// DataVault, SignalBus and dispatcher, i.e. the exact subsystems whose absence a failing run is
        /// usually trying to report. PublishCrashSignal has no try/catch at all. And because _finished is
        /// set before any of them, every catch in the startup path is disarmed by its own `if (!_finished)`
        /// guard, so a single throw in that stretch produced ZERO artifacts and no log line, permanently.
        ///
        /// That is exactly what a 45-minute run produced: no result JSON, no CSV rows, and total log
        /// silence, while the editor kept burning about 1.4 cores. Application.Quit is a no-op in the
        /// Editor, so play mode simply carried on running the main menu forever.
        ///
        /// A harness that cannot say "I failed" is worse than no harness, so the report is now the first
        /// side effect after the latch. Telemetry is best-effort after it.
        /// </remarks>
        private void CompleteAndQuit()
        {
            if (_finished)
                return;

            _finished = true;
            TryWriteResult(0, "SUCCESS");
            LogRunnerLifecycle("complete exitCode=0 status=SUCCESS");
            PublishCrashSignal(0, SuccessHash, 0);
            TryDumpBlackbox();
            Application.Quit(0);
        }

        /// <inheritdoc cref="CompleteAndQuit"/>
        private void FailAndQuit(int exitCode, uint reasonHash, string status)
        {
            if (_finished)
                return;

            _finished = true;
            TryWriteResult(exitCode, status);
            LogRunnerLifecycle("fail exitCode=" + exitCode.ToString(CultureInfo.InvariantCulture) + " status=" + status);
            RecordBlackbox(reasonHash);
            PublishCrashSignal(exitCode, reasonHash, 2);
            TryDumpBlackbox();
            Application.Quit(exitCode);
        }

        /// <summary>
        /// The only log surface this runner has. Deliberately unconditional and deliberately prefixed.
        /// </summary>
        /// <remarks>
        /// This file previously contained no Debug.Log of any kind, so a run that started and then died
        /// silently was indistinguishable from a run that never installed. Diagnosing one such run cost a
        /// 45-minute Unity session plus filesystem forensics on a zero-byte CSV, when a single grep for
        /// "[HEADLESS]" should have answered it. Called from three places only - install, the pre-wait
        /// gate, and the two quit paths - so it is cold, not cadence.
        /// </remarks>
        private static void LogRunnerLifecycle(string message)
        {
            // LogWarning, not Log, and that is the whole point of this method existing.
            // TryMarkEcologyReady sets Debug.unityLogger.filterLogType = LogType.Warning (:574) so
            // first-party Debug.Log spam cannot drown a 100-day batchmode log, and it is installed at :377
            // the instant the dispatcher wait succeeds. Unity drops LogType.Log at the managed Logger
            // BEFORE it reaches either the log file or Application.logMessageReceived - so that filter was
            // also eating this method's own verdict line. In the 2026-07-29 run, FailAndQuit wrote
            // [ECOLOGY_UNAVAILABLE] to the result JSON at :1025 and logged it at :1026; the JSON is on disk
            // and the string "[HEADLESS] fail" appears zero times in all 27,107 log lines. Two separate
            // investigations then read the resulting managed-log silence as a bootstrap deadlock and
            // diagnosed the wrong subsystem entirely, because the last surviving managed line happened to
            // be a GameBootstrapper node.
            //
            // A harness whose own verdict is filtered out by its own logging policy is worse than one that
            // never logged: it produces confident wrong answers instead of no answer. Lifecycle events are
            // cold (install, the pre-wait gate, and the two quit paths) so promoting them costs nothing at
            // cadence, and Warning is the correct severity for a line whose whole job is to survive.
            Debug.LogWarning("[HEADLESS] " + message);
        }

        private void PublishCrashSignal(int exitCode, uint reasonHash, byte severity)
        {
            SignalBus<CrashTelemetrySignal>.TryPushTracked(new CrashTelemetrySignal
            {
                SystemHash = RunnerHash,
                ReasonHash = reasonHash,
                Frame = SystemDispatcher.CurrentFrameId,
                ExitCode = exitCode,
                NativeAllocationCount = GlobalRegistry.NativeAllocationCount,
                NativeTrackedBytesMb = GlobalRegistry.NativeTrackedBytes * NativeBytesToMegabytes,
                Severity = severity,
                Flags = exitCode == 0 ? (byte)0 : (byte)1
            }, ref s_x001HeadlessSimulationRunnerSignalPushDropCount);
        }

        private void RecordBlackbox(uint flags)
        {
            if (!TryReadGhostState(out NativeArray<GhostState>.ReadOnly ghostState))
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !EnsureVaultBuffer(vault, ref _blackboxHandle, BlackboxBufferId, BlackboxFrameCapacity, NativeArrayOptions.ClearMemory) ||
                !vault.TryAcquireWriteLock(in _blackboxHandle, OwnerSystemId, out NativeArray<HeadlessTelemetryEntry> blackbox))
            {
                return;
            }

            try
            {
                if (!blackbox.IsCreated || blackbox.Length < BlackboxFrameCapacity)
                    return;

                GhostState state = ghostState[0];
                int index = _blackboxCursor % blackbox.Length;
                blackbox[index] = new HeadlessTelemetryEntry
                {
                    Frame = SystemDispatcher.CurrentFrameId,
                    Day = _completedDays,
                    StateHash = MixStateHash(in state),
                    GridX = state.Aup.GridX,
                    GridY = state.Aup.GridY,
                    GridZ = state.Aup.GridZ,
                    Local = new float3(state.Aup.LocalX, state.Aup.LocalY, state.Aup.LocalZ),
                    PreyBiomass = _lastPreyBiomass,
                    PredatorBiomass = _lastPredatorBiomass,
                    NativeBytesMb = GlobalRegistry.NativeTrackedBytes * NativeBytesToMegabytes,
                    Flags = flags
                };
                _blackboxCursor++;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackboxHandle, OwnerSystemId);
            }
        }

        private void TryDumpBlackbox()
        {
            try
            {
                DumpBlackbox();
            }
            catch (IOException)
            {
                _evidenceFailureFlags |= EvidenceBlackboxWriteFailed;
            }
            catch (UnauthorizedAccessException)
            {
                _evidenceFailureFlags |= EvidenceBlackboxWriteFailed;
            }
            catch (ArgumentException)
            {
                _evidenceFailureFlags |= EvidenceBlackboxWriteFailed;
            }
            catch (NotSupportedException)
            {
                _evidenceFailureFlags |= EvidenceBlackboxWriteFailed;
            }
        }

        private void DumpBlackbox()
        {
            if (!TryReadBlackbox(out NativeArray<HeadlessTelemetryEntry>.ReadOnly blackbox) || string.IsNullOrEmpty(_blackboxPath))
                return;

            EnsureParentDirectory(_blackboxPath);
            using (FileStream stream = new FileStream(_blackboxPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x48385142u);
                int validCount = math.min(_blackboxCursor, blackbox.Length);
                int start = _blackboxCursor >= blackbox.Length ? _blackboxCursor % blackbox.Length : 0;
                writer.Write(validCount);
                writer.Write(BlackboxEntrySizeBytes);
                writer.Write(_blackboxCursor);
                for (int i = 0; i < validCount; i++)
                {
                    int index = (start + i) % blackbox.Length;
                    HeadlessTelemetryEntry entry = blackbox[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Day);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.GridX);
                    writer.Write(entry.GridY);
                    writer.Write(entry.GridZ);
                    writer.Write(entry.Local.x);
                    writer.Write(entry.Local.y);
                    writer.Write(entry.Local.z);
                    writer.Write(entry.PreyBiomass);
                    writer.Write(entry.PredatorBiomass);
                    writer.Write(entry.NativeBytesMb);
                    writer.Write(entry.Flags);
                }
            }
        }

        private void WriteResult(int exitCode, string status)
        {
            if (string.IsNullOrEmpty(_resultPath))
                return;

            EnsureParentDirectory(_resultPath);
            string tempPath = _resultPath + ".tmp";
            using (StreamWriter writer = new StreamWriter(tempPath, false))
            {
                writer.Write('{');
                writer.Write("\"agent\":\"");
                writer.Write(RunnerName);
                writer.Write("\",\"status\":\"");
                writer.Write(status);
                writer.Write("\",\"exitCode\":");
                WriteInvariant(writer, exitCode);
                writer.Write(",\"days\":");
                WriteInvariant(writer, _completedDays);
                writer.Write(",\"targetDays\":");
                WriteInvariant(writer, _targetDays);
                writer.Write(",\"simulatedSeconds\":");
                WriteInvariant(writer, _simulatedSeconds);
                // Two dilations, and reporting only the first is how this harness lied about itself. The
                // nominal scalar is what Time.timeScale was set to; the delivered one is what the dispatcher
                // actually granted. SystemDispatcher.RunFastTick is a fixed-step substep loop capped at
                // MaxCadenceSubstepsPerFrame = 4 calls of FastTick(1/60) per frame, DISCARDING the overflow
                // (SystemDispatcher.cs:6245-6246), and this runner advances its day counter by that fixed
                // 1/60. So delivered dilation is 4 * fps / 60 = fps / 15, and reaching a nominal 100 would
                // need 1500 fps of full-world player loop. A reader who saw only "timeDilation": 100 would
                // conclude a 100-day run costs an hour; at a realistic batchmode frame rate it costs 7 to 25.
                // Compare the two fields to size the next run instead of trusting the configured one.
                writer.Write(",\"timeDilationNominal\":");
                WriteInvariant(writer, TimeDilationScalar);
                writer.Write(",\"timeDilationDelivered\":");
                WriteInvariant(writer, (float)DeliveredTimeDilation);
                writer.Write(",\"progressionSignals\":");
                WriteInvariant(writer, _progressionSignalCount);
                writer.Write(",\"crashSignalsConsumed\":");
                WriteInvariant(writer, _crashSignalCount);
                writer.Write(",\"lastProgressionHash\":");
                WriteInvariant(writer, _lastProgressionHash);
                writer.Write(",\"lastCrashReasonHash\":");
                WriteInvariant(writer, _lastCrashReasonHash);
                writer.Write(",\"syntheticAupShifts\":");
                WriteInvariant(writer, _lastSyntheticShiftSequence);
                writer.Write(",\"actualOriginShifts\":");
                WriteInvariant(writer, _actualOriginShiftCount);
                writer.Write(",\"nativeBytes\":");
                WriteInvariant(writer, _lastMemoryBytes);
                writer.Write(",\"h8Bytes\":");
                WriteInvariant(writer, _lastH8MemoryBytes);
                writer.Write(",\"gasInvalidRoomId\":");
                WriteInvariant(writer, _gasInvalidRoomId);
                // Days that produced a real biomass sample versus days that produced none. Reported because
                // the run can now COMPLETE with unsampled days in it: without these two numbers a reader
                // seeing status SUCCESS cannot tell a clean 100-day run from one where three day boundaries
                // landed inside a job fence, and the CSV would be the only place that fact survived.
                writer.Write(",\"ecologySampledDays\":");
                WriteInvariant(writer, _ecologySampledDayCount);
                writer.Write(",\"ecologyUnsampledDays\":");
                WriteInvariant(writer, _ecologyUnsampledDayCount);
                // Renamed from "logSpamSuppressed", which was a lie with consequences. HandleLogMessage
                // increments on LogType.Log messages it RECEIVES, so this has always been a DELIVERED count;
                // nothing in it is suppressed. In the 2026-07-29 run the value 18 was read as "18 messages
                // were hidden from you", which pointed the diagnosis at log volume when the actual problem
                // was the opposite: TryMarkEcologyReady sets filterLogType = LogType.Warning (:574)
                // and Unity drops LogType.Log at the managed Logger BEFORE Application.logMessageReceived,
                // so after that line installs this counter stops counting almost entirely. The honest reading
                // of a low number here is "the filter was already active", not "little spam happened".
                // Verified before renaming: no parser depends on the old key - the only other occurrence in
                // the repo is prose in BUILD_PLAYTEST_ISSUES.md quoting a historical artifact.
                writer.Write(",\"debugLogMessagesDelivered\":");
                WriteInvariant(writer, _debugLogDeliveredCount);
                writer.Write(",\"evidenceFailureFlags\":");
                WriteInvariant(writer, _evidenceFailureFlags);
                writer.Write('}');
            }

            PromoteResultFileCold(tempPath);
        }

        private void PromoteResultFileCold(string tempPath)
        {
            if (File.Exists(_resultPath))
                File.Replace(tempPath, _resultPath, null, true);
            else
                File.Move(tempPath, _resultPath);
        }

        private void TryWriteResult(int exitCode, string status)
        {
            try
            {
                WriteResult(exitCode, status);
            }
            catch (IOException)
            {
                _evidenceFailureFlags |= EvidenceResultWriteFailed;
            }
            catch (UnauthorizedAccessException)
            {
                _evidenceFailureFlags |= EvidenceResultWriteFailed;
            }
            catch (ArgumentException)
            {
                _evidenceFailureFlags |= EvidenceResultWriteFailed;
            }
            catch (NotSupportedException)
            {
                _evidenceFailureFlags |= EvidenceResultWriteFailed;
            }
        }

        /// <summary>
        /// Counts <see cref="LogType.Log"/> messages DELIVERED to this handler. Not a suppression count -
        /// see the field note in <see cref="WriteResult"/>.
        /// </summary>
        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
                _debugLogDeliveredCount++;
        }

        private static void TryDumpH8MemoryTable()
        {
            try
            {
                string dumpPath = ResolveProjectPath(H8MemoryDumpRelativePath);
                EnsureParentDirectory(dumpPath);
                H8Memory.DumpAllocationTableText(dumpPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureVaultBuffersCold()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            return EnsureVaultBuffer(vault, ref _ghostStateHandle, GhostStateBufferId, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultBuffer(vault, ref _blackboxHandle, BlackboxBufferId, BlackboxFrameCapacity, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultBuffer(vault, ref _memoryWindowBytesHandle, MemoryWindowBytesBufferId, MemoryWindowDays, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultBuffer(vault, ref _memoryWindowH8BytesHandle, MemoryWindowH8BytesBufferId, MemoryWindowDays, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultBuffer(vault, ref _memoryWindowAllocationCountsHandle, MemoryWindowAllocationCountsBufferId, MemoryWindowDays, NativeArrayOptions.ClearMemory);
        }

        private bool EnsureVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                options);

            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out existing) &&
                   existing.IsCreated &&
                   existing.Length >= requiredLength;
        }

        private bool TryInitializeGhostState()
        {
            if (_ghostStateInitialized)
                return true;

            if (!EnsureVaultBuffer(CacheDataVaultCold(), ref _ghostStateHandle, GhostStateBufferId, 1, NativeArrayOptions.ClearMemory))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _ghostStateHandle, OwnerSystemId, out NativeArray<GhostState> ghostState))
            {
                return false;
            }

            try
            {
                if (!ghostState.IsCreated || ghostState.Length <= 0)
                    return false;

                GhostState initial = default;
                initial.Aup = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
                initial.AbsoluteMeters = double3.zero;
                initial.RuntimeMeters = float3.zero;
                ghostState[0] = initial;
                _ghostStateInitialized = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _ghostStateHandle, OwnerSystemId);
            }
        }

        private bool TryReadGhostState(out NativeArray<GhostState>.ReadOnly ghostState)
        {
            ghostState = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _ghostStateHandle, GhostStateBufferId) &&
                   vault.TryReadOnlyHandle(in _ghostStateHandle, out ghostState) &&
                   ghostState.IsCreated &&
                   ghostState.Length > 0;
        }

        private bool TryCommitPendingGhostState(out GhostState previous, out GhostState next)
        {
            previous = default;
            next = _pendingGhostState;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _ghostStateHandle, GhostStateBufferId) ||
                !vault.TryAcquireWriteLock(in _ghostStateHandle, OwnerSystemId, out NativeArray<GhostState> ghostState))
            {
                return false;
            }

            try
            {
                if (!ghostState.IsCreated || ghostState.Length <= 0)
                    return false;

                previous = ghostState[0];
                ghostState[0] = next;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _ghostStateHandle, OwnerSystemId);
            }
        }

        private bool TryReadBlackbox(out NativeArray<HeadlessTelemetryEntry>.ReadOnly blackbox)
        {
            blackbox = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _blackboxHandle, BlackboxBufferId) &&
                   vault.TryReadOnlyHandle(in _blackboxHandle, out blackbox) &&
                   blackbox.IsCreated &&
                   blackbox.Length >= BlackboxFrameCapacity;
        }

        private void ReleaseVaultBuffers()
        {
            ReleaseVaultBuffer(ref _memoryWindowAllocationCountsHandle);
            ReleaseVaultBuffer(ref _memoryWindowH8BytesHandle);
            ReleaseVaultBuffer(ref _memoryWindowBytesHandle);
            ReleaseVaultBuffer(ref _blackboxHandle);
            ReleaseVaultBuffer(ref _ghostStateHandle);
            _ghostStateInitialized = false;
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static bool ShouldRunStatic()
        {
            if (HasCommandLineArg(CommandLineArg) || HasCommandLineArg(LegacyCommandLineArg))
                return true;

            string value = global::System.Environment.GetEnvironmentVariable(EnvironmentFlagName);
            if (string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;

            return File.Exists(ResolveProjectPathStatic(FlagRelativePath));
        }

        private static bool HasCommandLineArg(string commandLineArg)
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], commandLineArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static int TryReadInt(string[] args, string name, int fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                    TryParseCommandLineInt(args[i + 1], out int value))
                    return value;
            }

            return fallback;
        }

        private static float TryReadFloat(string[] args, string name, float fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                    TryParseCommandLineFloat(args[i + 1], out float value))
                    return value;
            }

            return fallback;
        }

        private static bool TryParseCommandLineInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int index = 0;
            int sign = 1;
            if (text[0] == '-')
            {
                sign = -1;
                index = 1;
            }
            else if (text[0] == '+')
            {
                index = 1;
            }

            int result = 0;
            bool any = false;
            for (; index < text.Length; index++)
            {
                char c = text[index];
                if (c < '0' || c > '9')
                    return false;
                result = (result * 10) + (c - '0');
                any = true;
            }

            value = result * sign;
            return any;
        }

        private static bool TryParseCommandLineFloat(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int index = 0;
            float sign = 1f;
            if (text[0] == '-')
            {
                sign = -1f;
                index = 1;
            }
            else if (text[0] == '+')
            {
                index = 1;
            }

            float integer = 0f;
            bool any = false;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                integer = (integer * 10f) + (text[index] - '0');
                index++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < text.Length && text[index] == '.')
            {
                index++;
                while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                {
                    fraction = (fraction * 10f) + (text[index] - '0');
                    divisor *= 10f;
                    index++;
                    any = true;
                }
            }

            if (index != text.Length || !any)
                return false;

            value = sign * (integer + fraction / divisor);
            return math.isfinite(value);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return ResolveProjectPathStatic(relativePath);
        }

        private static string ResolveProjectPathStatic(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        private static int ClampGrid(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private static uint MixStateHash(in GhostState state)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)state.Aup.GridX) * 16777619u;
                hash = (hash ^ (uint)(state.Aup.GridX >> 32)) * 16777619u;
                hash = (hash ^ (uint)state.Aup.GridY) * 16777619u;
                hash = (hash ^ (uint)(state.Aup.GridY >> 32)) * 16777619u;
                hash = (hash ^ (uint)state.Aup.GridZ) * 16777619u;
                hash = (hash ^ (uint)(state.Aup.GridZ >> 32)) * 16777619u;
                hash = (hash ^ math.asuint(state.Aup.LocalX)) * 16777619u;
                hash = (hash ^ math.asuint(state.Aup.LocalY)) * 16777619u;
                hash = (hash ^ math.asuint(state.Aup.LocalZ)) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        private static void WriteInvariant(StreamWriter writer, int value)
        {
            Span<char> scratch = stackalloc char[16];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, long value)
        {
            Span<char> scratch = stackalloc char[32];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, uint value)
        {
            Span<char> scratch = stackalloc char[16];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, float value)
        {
            Span<char> scratch = stackalloc char[32];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, double value)
        {
            Span<char> scratch = stackalloc char[32];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        [StructLayout(LayoutKind.Explicit, Size = 88)]
        private struct GhostState
        {
            [FieldOffset(0)] public AbsoluteUniversePosition Aup;
            [FieldOffset(48)] public double3 AbsoluteMeters;
            [FieldOffset(72)] public float3 RuntimeMeters;
            [FieldOffset(84)] private uint _pad0;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct HeadlessTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public long GridX;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public long GridY;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public long GridZ;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public int Day;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public float3 Local;
            [System.Runtime.InteropServices.FieldOffset(48)]
            public float PreyBiomass;
            [System.Runtime.InteropServices.FieldOffset(52)]
            public float PredatorBiomass;
            [System.Runtime.InteropServices.FieldOffset(56)]
            public float NativeBytesMb;
            [System.Runtime.InteropServices.FieldOffset(60)]
            public uint Flags;
        }

        private static GhostState ResolveNextGhostState(
            in GhostState current,
            float deltaSeconds,
            double simulatedSeconds,
            float speedMetersPerSecond)
        {
            GhostState state = current;
            double3 position = state.Aup.ToAbsoluteDouble3();
            float t = (float)(simulatedSeconds * 0.001d);
            uint baseSeed = math.asuint(t) ^ 0x9E3779B9u;
            float3 direction = new float3(
                HashSignedUnit(baseSeed ^ 0xA2F12B91u),
                HashSignedUnit(baseSeed ^ 0x3D20ADEAu) * 0.12f,
                HashSignedUnit(baseSeed ^ 0x7F4A7C15u));
            float lengthSq = math.lengthsq(direction);
            direction = lengthSq > 0.0001f ? direction * math.rsqrt(lengthSq) : new float3(1f, 0f, 0f);
            position += (double3)(direction * (speedMetersPerSecond * math.max(0f, deltaSeconds)));
            state.AbsoluteMeters = position;
            state.Aup = AbsoluteUniversePosition.FromAbsolutePosition(position);
            state.RuntimeMeters = new float3(state.Aup.LocalX, state.Aup.LocalY, state.Aup.LocalZ);
            return state;
        }

        private static float HashSignedUnit(uint seed)
        {
            uint h = seed * 747796405u + 2891336453u;
            h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
            h = (h >> 22) ^ h;
            return ((h & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
        }

        private sealed class HeadlessCsvWriter : IDisposable
        {
            private readonly FileStream _stream;
            private readonly byte[] _buffer;
            private int _cursor;
            private bool _overflowed;

            public HeadlessCsvWriter(string path)
            {
                _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096);
                // COLD ALLOC: byte[512] - fixed ASCII CSV row staging buffer, flushed only on daily audit cold path - owner: HeadlessCsvWriter
                _buffer = new byte[512];
            }

            public void WriteHeader()
            {
                AppendAscii("Day,PreyBiomass,PredatorBiomass,CarryingCapacity,NativeBytes,H8Bytes,NativeAllocations,H8Allocations,Flags\n");
                Flush();
            }

            public bool WriteDay(
                int day,
                float prey,
                float predator,
                float capacity,
                long nativeBytes,
                long h8Bytes,
                int nativeAllocations,
                int h8Allocations,
                uint flags)
            {
                _cursor = 0;
                _overflowed = false;
                AppendInt(day);
                AppendComma();
                AppendFixed(prey);
                AppendComma();
                AppendFixed(predator);
                AppendComma();
                AppendFixed(capacity);
                AppendComma();
                AppendLong(nativeBytes);
                AppendComma();
                AppendLong(h8Bytes);
                AppendComma();
                AppendInt(nativeAllocations);
                AppendComma();
                AppendInt(h8Allocations);
                AppendComma();
                AppendUInt(flags);
                AppendByte((byte)'\n');
                return Flush();
            }

            public void Dispose()
            {
                try
                {
                    _ = Flush();
                }
                catch (IOException)
                {
                    _cursor = 0;
                }
                catch (ObjectDisposedException)
                {
                    _cursor = 0;
                }
                catch (NotSupportedException)
                {
                    _cursor = 0;
                }

                try
                {
                    _stream.Dispose();
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            public void DiscardPendingRow()
            {
                _cursor = 0;
                _overflowed = false;
            }

            private bool Flush()
            {
                if (_overflowed)
                {
                    _cursor = 0;
                    _overflowed = false;
                    return false;
                }

                if (_cursor <= 0)
                    return true;

                _stream.Write(_buffer, 0, _cursor);

                // Flush(true), not Flush(), and the difference is two claims the old comment conflated.
                //
                // Flush() moves the managed buffer into the OS file cache, which is ALREADY enough to survive
                // the process being killed - the kernel still holds the bytes. So "a run that is killed loses
                // all of its evidence" was wrong. What Flush() does NOT do is call FlushFileBuffers, so
                // (a) a machine crash or power loss between day rows loses them, and (b) Windows does not
                // update the visible directory-entry SIZE while this handle is open. (b) is the one that
                // actually cost time: a zero-byte CSV read mid-run was taken as proof that no days had
                // completed, when days had completed and the size simply was not visible yet. Flush(true)
                // closes both.
                //
                // Affordable purely because of cadence, which is also why this is not a hot-path violation:
                // Flush() is reached from TryWriteDailyCsv, called once per SIMULATED DAY - roughly 100
                // fsyncs across the 100-day default, not one per frame. Worth it, because these allocation
                // counts exist ONLY in this CSV and in no other artifact, so a lost row is a lost
                // measurement rather than a lost copy.
                _stream.Flush(true);
                _cursor = 0;
                return true;
            }

            private void AppendComma()
            {
                AppendByte((byte)',');
            }

            private void AppendAscii(string value)
            {
                for (int i = 0; i < value.Length; i++)
                    AppendByte((byte)value[i]);
            }

            private void AppendFixed(float value)
            {
                if (!math.isfinite(value))
                {
                    AppendAscii("nan");
                    return;
                }

                if (value < 0f)
                {
                    AppendByte((byte)'-');
                    value = -value;
                }

                long milli = (long)math.round(value * 1000f);
                AppendLong(milli / 1000L);
                AppendByte((byte)'.');
                int frac = (int)(milli % 1000L);
                AppendByte((byte)('0' + (frac / 100) % 10));
                AppendByte((byte)('0' + (frac / 10) % 10));
                AppendByte((byte)('0' + frac % 10));
            }

            private void AppendInt(int value)
            {
                AppendLong(value);
            }

            private void AppendUInt(uint value)
            {
                if (value == 0u)
                {
                    AppendByte((byte)'0');
                    return;
                }

                int start = _cursor;
                while (value > 0u)
                {
                    AppendByte((byte)('0' + value % 10u));
                    value /= 10u;
                }

                Reverse(start, _cursor - 1);
            }

            private void AppendLong(long value)
            {
                if (value == 0L)
                {
                    AppendByte((byte)'0');
                    return;
                }

                if (value < 0L)
                {
                    AppendByte((byte)'-');
                    value = -value;
                }

                int start = _cursor;
                while (value > 0L)
                {
                    AppendByte((byte)('0' + value % 10L));
                    value /= 10L;
                }

                Reverse(start, _cursor - 1);
            }

            private void AppendByte(byte value)
            {
                if (_overflowed)
                    return;

                if (_cursor >= _buffer.Length)
                {
                    _overflowed = true;
                    return;
                }

                _buffer[_cursor++] = value;
            }

            private void Reverse(int first, int last)
            {
                if (_overflowed)
                    return;

                while (first < last)
                {
                    byte temp = _buffer[first];
                    _buffer[first] = _buffer[last];
                    _buffer[last] = temp;
                    first++;
                    last--;
                }
            }
        }
    }
}
