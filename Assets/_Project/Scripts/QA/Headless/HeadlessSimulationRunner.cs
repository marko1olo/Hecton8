using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.QA.Headless
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class HeadlessSimulationRunner : MonoBehaviour, IFastTickable, IFrostTickable, IColdTickable, ILateFrameTickable, IOriginShiftListener
    {
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
        private const string BlackboxRelativePath = "Docs/AgentLogs/Dump_HEADLESS_SIMULATION_RUNNER.bin";
        private const int BlackboxFrameCapacity = 300;
        private const int BlackboxEntrySizeBytes = 64;
        private const int MemoryWindowDays = 10;
        private const int MaxSignalsDrainedPerFrame = 128;
        private const int MaxDailyAuditsPerFrostTick = 4;
        private const int DefaultTargetDays = 100;
        private const int AupCellSizeMeters = 5000;
        private const float DefaultDaySeconds = 3600f;
        private const float DefaultStartupTimeoutSeconds = 180f;
        private const float TimeDilationScalar = 100f;
        private const float GhostSpeedMetersPerSecond = 85f;
        private const float NativeBytesToMegabytes = 1f / (1024f * 1024f);
        private const uint RunnerHash = 0x48385141u;
        private const uint SuccessHash = 0x48385130u;
        private const uint LeakHash = 0x48384C45u;
        private const uint EcologyCollapseHash = 0x48384543u;
        private const uint GasInvalidHash = 0x48384741u;
        private const uint NaNHash = 0x48384E41u;
        private const uint TimeoutHash = 0x4838544Fu;
        private const uint AupShiftHash = 0x48384155u;
        private const uint CsvWriteHash = 0x48384353u;
        private const uint EvidenceBlackboxWriteFailed = 1u << 0;
        private const uint EvidenceResultWriteFailed = 1u << 1;
        private const uint EvidenceCsvWriteFailed = 1u << 2;

        private static HeadlessSimulationRunner _instance;

        private NativeArray<GhostState> _ghostState;
        private NativeArray<GhostState> _ghostNextState;
        private NativeArray<HeadlessTelemetryEntry> _blackbox;
        private NativeArray<long> _memoryWindowBytes;
        private NativeArray<long> _memoryWindowH8Bytes;
        private NativeArray<int> _memoryWindowAllocationCounts;
        private JobHandle _ghostJobHandle;
        private HeadlessCsvWriter _csvWriter;
        private string _resultPath;
        private string _blackboxPath;
        private double _ghostSeconds;
        private double _simulatedSeconds;
        private double _dayAccumulatorSeconds;
        private double _startupTime;
        private float _daySeconds = DefaultDaySeconds;
        private float _startupTimeoutSeconds = DefaultStartupTimeoutSeconds;
        private int _targetDays = DefaultTargetDays;
        private int _completedDays;
        private int _memoryWindowCursor;
        private int _memoryWindowCount;
        private int _blackboxCursor;
        private int _progressionSignalCount;
        private int _crashSignalCount;
        private int _gasInvalidRoomId = -1;
        private int _logSpamCount;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private int _previousCaptureFramerate;
        private long _lastMemoryBytes;
        private long _lastH8MemoryBytes;
        private float _lastPreyBiomass;
        private float _lastPredatorBiomass;
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
        private bool _originListenerRegistered;
        private bool _ghostJobPending;
        private bool _ecologyReady;
        private bool _finished;
        private bool _runtimePolicyCaptured;

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
            _ = RunStartupAsync(destroyCancellationToken);
        }

        private async Awaitable RunStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                InitializeColdState();
                await WaitForDispatcherAndStart(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }

        private void OnDestroy()
        {
            if (_ghostJobPending)
            {
                DisposeNativeArray(ref _ghostState, _ghostJobHandle);
                DisposeNativeArray(ref _ghostNextState, _ghostJobHandle);
                _ghostJobPending = false;
            }
            else
            {
                DisposeNativeArray(ref _ghostState);
                DisposeNativeArray(ref _ghostNextState);
            }

            if (_registeredFast)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Core);
            if (_registeredFrost)
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Core);
            if (_registeredCold)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Core);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            if (_originListenerRegistered)
                HectonFloatingOrigin.UnregisterListener(this);
            Application.logMessageReceived -= HandleLogMessage;
            RestoreRuntimePolicy();

            DisposeNativeArray(ref _blackbox);
            DisposeNativeArray(ref _memoryWindowBytes);
            DisposeNativeArray(ref _memoryWindowH8Bytes);
            DisposeNativeArray(ref _memoryWindowAllocationCounts);
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

            if (!_ghostJobPending)
            {
                GhostAupJob job = new GhostAupJob
                {
                    Current = _ghostState,
                    Next = _ghostNextState,
                    DeltaSeconds = safeDelta,
                    SimulatedSeconds = _ghostSeconds,
                    SpeedMetersPerSecond = GhostSpeedMetersPerSecond
                };
                _ghostJobHandle = job.Schedule();
                _ghostJobPending = true;
            }

            RecordBlackbox(0u);
        }

        public void LateFrameTick()
        {
            if (!_started || _finished || !_ghostJobPending || !_ghostJobHandle.IsCompleted)
                return;

            _ghostJobHandle.Complete();
            _ghostJobPending = false;
            GhostState previous = _ghostState[0];
            GhostState next = _ghostNextState[0];
            _ghostState[0] = next;
            HandleSyntheticAupShift(in previous, in next);
            if (!math.all(math.isfinite(next.AbsoluteMeters)) ||
                !math.isfinite(next.RuntimeMeters.x) ||
                !math.isfinite(next.RuntimeMeters.y) ||
                !math.isfinite(next.RuntimeMeters.z))
            {
                FailAndQuit(1, NaNHash, "[NAN_DETECTED]");
            }
        }

        public void FrostTick()
        {
            if (!_started || _finished)
                return;

            TryMarkEcologyReady();
            if (!_ecologyReady)
                return;

            if (!AuditGasPressureFinite())
            {
                FailAndQuit(1, GasInvalidHash, "[GAS_INVALID]");
                return;
            }

            int auditsThisTick = 0;
            while (_dayAccumulatorSeconds >= _daySeconds &&
                   _completedDays < _targetDays &&
                   auditsThisTick < MaxDailyAuditsPerFrostTick &&
                   !_finished)
            {
                _dayAccumulatorSeconds -= _daySeconds;
                ExecuteDailyAudit();
                auditsThisTick++;
            }
        }

        public void ColdTick()
        {
            if (_finished)
                return;

            if (!_started)
                return;

            if (!_ecologyReady && Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)
            {
                FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _actualOriginShiftCount++;
        }

        private async Awaitable WaitForDispatcherAndStart(CancellationToken cancellationToken)
        {
            _startupTime = Time.realtimeSinceStartupAsDouble;
            while (GlobalRegistry.Dispatcher == null && Time.realtimeSinceStartupAsDouble - _startupTime <= _startupTimeoutSeconds)
            {
                if (cancellationToken.IsCancellationRequested || _finished)
                    return;

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || _finished)
                return;

            if (GlobalRegistry.Dispatcher == null)
            {
                FailAndQuit(1, TimeoutHash, "[DISPATCHER_TIMEOUT]");
                return;
            }

            ForceHeadlessRuntimePolicy();
            _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Core);
            _registeredFrost = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Core);
            _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            HectonFloatingOrigin.RegisterListener(this);
            _originListenerRegistered = true;
            GlobalRegistry.TickDispatcher?.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
            _started = _registeredFast && _registeredFrost && _registeredCold && _registeredLate;
            if (!_started)
                FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
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
            // COLD ALLOC: NativeArray<GhostState>[1] - front ghost AUP state for the headless math-only player - owner: HeadlessSimulationRunner
            _ghostState = new NativeArray<GhostState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<GhostState>[1] - back ghost AUP state written by GhostAupJob - owner: HeadlessSimulationRunner
            _ghostNextState = new NativeArray<GhostState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<HeadlessTelemetryEntry>[300] - fixed blackbox ring for crash/postmortem state - owner: HeadlessSimulationRunner
            _blackbox = new NativeArray<HeadlessTelemetryEntry>(BlackboxFrameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<long>[10] - daily native-byte growth window for leak detection - owner: HeadlessSimulationRunner
            _memoryWindowBytes = new NativeArray<long>(MemoryWindowDays, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<long>[10] - daily H8 byte growth window for leak detection - owner: HeadlessSimulationRunner
            _memoryWindowH8Bytes = new NativeArray<long>(MemoryWindowDays, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[10] - daily H8 allocation-count growth window for leak detection - owner: HeadlessSimulationRunner
            _memoryWindowAllocationCounts = new NativeArray<int>(MemoryWindowDays, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_ghostState, nameof(_ghostState));
            RegisterNativeArray(_ghostNextState, nameof(_ghostNextState));
            RegisterNativeArray(_blackbox, nameof(_blackbox));
            RegisterNativeArray(_memoryWindowBytes, nameof(_memoryWindowBytes));
            RegisterNativeArray(_memoryWindowH8Bytes, nameof(_memoryWindowH8Bytes));
            RegisterNativeArray(_memoryWindowAllocationCounts, nameof(_memoryWindowAllocationCounts));
            _csvWriter = new HeadlessCsvWriter(csvPath);
            _csvWriter.WriteHeader();
            GhostState initial = default;
            initial.Aup = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
            _ghostState[0] = initial;
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
            Debug.unityLogger.filterLogType = LogType.Warning;
            GlobalRegistry.RegisterScalabilityTierOverride(1);
            GlobalRegistry.RegisterMathPrecisionLevel(MathPrecisionLevel.High);
            DistanceMath.PushShaderMathLod(MathLodMode.High);
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

        private void TryMarkEcologyReady()
        {
            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            _ecologyReady = ecosystem != null && ecosystem.IsInitialized;
        }

        private void DrainSignals()
        {
            int drained = 0;
            while (drained < MaxSignalsDrainedPerFrame && GlobalSignals.TryDequeueProgressionEvent(out ProgressionEventSignal progression))
            {
                _progressionSignalCount++;
                _lastProgressionHash = progression.PoiHash != 0u ? progression.PoiHash : progression.QuestHash;
                drained++;
            }

            drained = 0;
            while (drained < MaxSignalsDrainedPerFrame && GlobalSignals.TryDequeueCrashTelemetry(out CrashTelemetrySignal crash))
            {
                _crashSignalCount++;
                _lastCrashReasonHash = crash.ReasonHash;
                drained++;
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

            if (DetectTenDayMemoryGrowth(nativeBytes, h8Bytes, h8Allocations))
            {
                FailAndQuit(1, LeakHash, "[LEAK_DETECTED]");
                return;
            }

            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            if (ecosystem == null || !ecosystem.TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample biomass))
            {
                if (!TryWriteDailyCsv(default, nativeBytes, h8Bytes, nativeAllocations, h8Allocations, flags: 1u))
                    return;

                FailAndQuit(1, EcologyCollapseHash, "[ECOLOGY_UNAVAILABLE]");
                return;
            }

            _lastPreyBiomass = biomass.PreyBiomassSum;
            _lastPredatorBiomass = biomass.PredatorBiomassSum;
            if (!TryWriteDailyCsv(biomass, nativeBytes, h8Bytes, nativeAllocations, h8Allocations, biomass.Flags))
                return;

            if (biomass.PredatorBiomassSum <= 0f)
            {
                FailAndQuit(1, EcologyCollapseHash, "[ECOLOGY_COLLAPSE]");
                return;
            }

            if (_completedDays >= _targetDays)
                CompleteAndQuit();
        }

        private bool AuditGasPressureFinite()
        {
            IGasDynamicsSolver gas = GlobalRegistry.GasDynamics;
            if (gas == null || !gas.IsInitialized)
                return true;

            NativeArray<float>.ReadOnly pressures = gas.RoomPressure;
            int count = math.min(gas.RoomCount, pressures.Length);
            for (int i = 0; i < count; i++)
            {
                float pressure = pressures[i];
                if (!math.isfinite(pressure) || pressure < 0f)
                {
                    _gasInvalidRoomId = i;
                    return false;
                }
            }

            return true;
        }

        private bool DetectTenDayMemoryGrowth(long nativeBytes, long h8Bytes, int h8Allocations)
        {
            int slot = _memoryWindowCursor % MemoryWindowDays;
            _memoryWindowBytes[slot] = nativeBytes;
            _memoryWindowH8Bytes[slot] = h8Bytes;
            _memoryWindowAllocationCounts[slot] = h8Allocations;
            _memoryWindowCursor++;
            _memoryWindowCount = math.min(_memoryWindowCount + 1, MemoryWindowDays);
            if (_memoryWindowCount < MemoryWindowDays)
                return false;

            return HasStrictMemoryGrowth(_memoryWindowBytes) ||
                   HasStrictMemoryGrowth(_memoryWindowH8Bytes) ||
                   HasStrictAllocationGrowth();
        }

        private bool HasStrictMemoryGrowth(NativeArray<long> samples)
        {
            long previousBytes = samples[_memoryWindowCursor % MemoryWindowDays];
            for (int i = 1; i < MemoryWindowDays; i++)
            {
                int index = (_memoryWindowCursor + i) % MemoryWindowDays;
                long currentBytes = samples[index];
                if (currentBytes <= previousBytes)
                    return false;

                previousBytes = currentBytes;
            }

            return true;
        }

        private bool HasStrictAllocationGrowth()
        {
            int previousCount = _memoryWindowAllocationCounts[_memoryWindowCursor % MemoryWindowDays];
            for (int i = 1; i < MemoryWindowDays; i++)
            {
                int index = (_memoryWindowCursor + i) % MemoryWindowDays;
                int currentCount = _memoryWindowAllocationCounts[index];
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
                _csvWriter.WriteDay(
                    _completedDays,
                    biomass.PreyBiomassSum,
                    biomass.PredatorBiomassSum,
                    biomass.CarryingCapacitySum,
                    nativeBytes,
                    h8Bytes,
                    nativeAllocations,
                    h8Allocations,
                    flags);
                return true;
            }
            catch (Exception)
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
            GlobalSignals.Publish(new AupPreShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            GlobalSignals.Publish(new RebaseSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                GridDelta = delta,
                Flags = 1u
            });
            GlobalSignals.Publish(new AupShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            RecordBlackbox(AupShiftHash);
        }

        private void CompleteAndQuit()
        {
            if (_finished)
                return;

            _finished = true;
            PublishCrashSignal(0, SuccessHash, 0);
            TryDumpBlackbox();
            TryWriteResult(0, "SUCCESS");
            Application.Quit(0);
        }

        private void FailAndQuit(int exitCode, uint reasonHash, string status)
        {
            if (_finished)
                return;

            _finished = true;
            RecordBlackbox(reasonHash);
            PublishCrashSignal(exitCode, reasonHash, 2);
            TryDumpBlackbox();
            TryWriteResult(exitCode, status);
            Application.Quit(exitCode);
        }

        private void PublishCrashSignal(int exitCode, uint reasonHash, byte severity)
        {
            GlobalSignals.Publish(new CrashTelemetrySignal
            {
                SystemHash = RunnerHash,
                ReasonHash = reasonHash,
                Frame = unchecked((uint)Time.frameCount),
                ExitCode = exitCode,
                NativeAllocationCount = GlobalRegistry.NativeAllocationCount,
                NativeTrackedBytesMb = GlobalRegistry.NativeTrackedBytes * NativeBytesToMegabytes,
                Severity = severity,
                Flags = exitCode == 0 ? (byte)0 : (byte)1
            });
        }

        private void RecordBlackbox(uint flags)
        {
            if (!_blackbox.IsCreated || !_ghostState.IsCreated)
                return;

            GhostState state = _ghostState[0];
            int index = _blackboxCursor % _blackbox.Length;
            _blackbox[index] = new HeadlessTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
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

        private void TryDumpBlackbox()
        {
            try
            {
                DumpBlackbox();
            }
            catch (Exception)
            {
                _evidenceFailureFlags |= EvidenceBlackboxWriteFailed;
            }
        }

        private void DumpBlackbox()
        {
            if (!_blackbox.IsCreated || string.IsNullOrEmpty(_blackboxPath))
                return;

            EnsureParentDirectory(_blackboxPath);
            using (FileStream stream = new FileStream(_blackboxPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x48385142u);
                int validCount = math.min(_blackboxCursor, _blackbox.Length);
                int start = _blackboxCursor >= _blackbox.Length ? _blackboxCursor % _blackbox.Length : 0;
                writer.Write(validCount);
                writer.Write(BlackboxEntrySizeBytes);
                writer.Write(_blackboxCursor);
                for (int i = 0; i < validCount; i++)
                {
                    int index = (start + i) % _blackbox.Length;
                    HeadlessTelemetryEntry entry = _blackbox[index];
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
                writer.Write(",\"timeDilation\":");
                WriteInvariant(writer, TimeDilationScalar);
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
                writer.Write(",\"logSpamSuppressed\":");
                WriteInvariant(writer, _logSpamCount);
                writer.Write(",\"evidenceFailureFlags\":");
                WriteInvariant(writer, _evidenceFailureFlags);
                writer.Write('}');
            }

            if (File.Exists(_resultPath))
                File.Delete(_resultPath);
            File.Move(tempPath, _resultPath);
        }

        private void TryWriteResult(int exitCode, string status)
        {
            try
            {
                WriteResult(exitCode, status);
            }
            catch (Exception)
            {
                _evidenceFailureFlags |= EvidenceResultWriteFailed;
            }
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
                _logSpamCount++;
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
                    int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    return value;
            }

            return fallback;
        }

        private static float TryReadFloat(string[] args, string name, float fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    return value;
            }

            return fallback;
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

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, RunnerName, label, NativeAllocationLifetime.Session);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GhostState
        {
            public AbsoluteUniversePosition Aup;
            public double3 AbsoluteMeters;
            public float3 RuntimeMeters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HeadlessTelemetryEntry
        {
            public uint Frame;
            public int Day;
            public uint StateHash;
            public long GridX;
            public long GridY;
            public long GridZ;
            public float3 Local;
            public float PreyBiomass;
            public float PredatorBiomass;
            public float NativeBytesMb;
            public uint Flags;
        }

        [BurstCompile]
        private struct GhostAupJob : IJob
        {
            [ReadOnly]
            public NativeArray<GhostState> Current;
            [WriteOnly]
            public NativeArray<GhostState> Next;
            public float DeltaSeconds;
            public double SimulatedSeconds;
            public float SpeedMetersPerSecond;

            public void Execute()
            {
                GhostState state = Current[0];
                double3 position = state.Aup.ToAbsoluteDouble3();
                float t = (float)(SimulatedSeconds * 0.001);
                float3 noiseProbe = new float3(t, t * 0.73f + 17.1f, t * 1.37f + 31.7f);
                float3 direction = new float3(
                    noise.cnoise(noiseProbe),
                    noise.cnoise(noiseProbe + new float3(19.1f, 3.7f, 11.2f)) * 0.12f,
                    noise.cnoise(noiseProbe + new float3(5.3f, 29.4f, 41.9f)));
                float lengthSq = math.lengthsq(direction);
                direction = lengthSq > 0.0001f ? direction * math.rsqrt(lengthSq) : new float3(1f, 0f, 0f);
                position += (double3)(direction * (SpeedMetersPerSecond * math.max(0f, DeltaSeconds)));
                state.AbsoluteMeters = position;
                state.Aup = AbsoluteUniversePosition.FromAbsolutePosition(position);
                state.RuntimeMeters = new float3(state.Aup.LocalX, state.Aup.LocalY, state.Aup.LocalZ);
                Next[0] = state;
            }
        }

        private sealed class HeadlessCsvWriter : IDisposable
        {
            private readonly FileStream _stream;
            private readonly byte[] _buffer;
            private int _cursor;

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

            public void WriteDay(
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
                Flush();
            }

            public void Dispose()
            {
                try
                {
                    Flush();
                }
                catch (Exception)
                {
                    _cursor = 0;
                }

                try
                {
                    _stream.Dispose();
                }
                catch (Exception)
                {
                }
            }

            public void DiscardPendingRow()
            {
                _cursor = 0;
            }

            private void Flush()
            {
                if (_cursor <= 0)
                    return;

                _stream.Write(_buffer, 0, _cursor);
                _cursor = 0;
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
                if (_cursor >= _buffer.Length)
                    throw new InvalidOperationException("HEADLESS_CSV_ROW_OVERFLOW");

                _buffer[_cursor++] = value;
            }

            private void Reverse(int first, int last)
            {
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
