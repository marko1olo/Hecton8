using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

#if UNITY_EDITOR
namespace Hecton8.QA.Headless
{
    public enum Shinobu38QaTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct WatchdogStateDTO
    {
        [FieldOffset(0)] public double3 CurrentTargetAUP;
        [FieldOffset(24)] public float DistanceTraveled;
        [FieldOffset(28)] public uint ErrorCount;
        [FieldOffset(32)] public float TestDuration;
        [FieldOffset(36)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TelemetrySnapshotDTO
    {
        [FieldOffset(0)] public float FrameTimeMs;
        [FieldOffset(4)] public float GcAllocBytes;
        [FieldOffset(8)] public float VramUsed;
        [FieldOffset(12)] public float AupJitterError;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Shinobu38RouteWaypointDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockRebaseSignal
    {
        [FieldOffset(0)] public double3 OffsetAUP;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Shinobu38TuningDTO
    {
        [FieldOffset(0)] public float SwimSpeed;
        [FieldOffset(4)] public float ObstacleAvoidanceStrength;
        [FieldOffset(8)] public float TelemetryWriteFrequency;
        [FieldOffset(12)] public float FastForwardScale;
        [FieldOffset(16)] public uint Tier;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public uint _pad1;
        [FieldOffset(28)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct Shinobu38MockVaultDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public int CurrentWaypointIndex;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint FrameFlags;
        [FieldOffset(36)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct Shinobu38WatchdogTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public float TargetDistanceRemaining;
        [FieldOffset(8)] public float AvoidanceCorrections;
        [FieldOffset(12)] public float CsvWriteTimeMs;
        [FieldOffset(16)] public int LocalMillimetersX;
        [FieldOffset(20)] public int LocalMillimetersY;
        [FieldOffset(24)] public int LocalMillimetersZ;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public int SectorX;
        [FieldOffset(36)] public int SectorY;
        [FieldOffset(40)] public int SectorZ;
        [FieldOffset(44)] public uint ShiftFrameId;
        [FieldOffset(48)] public uint AupHash;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Shinobu38FileWriteCommand
    {
        [FieldOffset(0)] public long Sequence;
        [FieldOffset(8)] public int PayloadOffset;
        [FieldOffset(12)] public int PayloadLength;
        [FieldOffset(16)] public uint Target;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct Shinobu38FileWriterStateDTO
    {
        [FieldOffset(0)] public long LastWriteTicks;
        [FieldOffset(8)] public int LastCsvWriteMicros;
        [FieldOffset(12)] public int LastAnyWriteMicros;
        [FieldOffset(16)] public uint CompletedWrites;
        [FieldOffset(20)] public uint WriterFlags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
        [FieldOffset(32)] public uint _pad2;
        [FieldOffset(36)] public uint _pad3;
        [FieldOffset(40)] public uint _pad4;
        [FieldOffset(44)] public uint _pad5;
        [FieldOffset(48)] public uint _pad6;
        [FieldOffset(52)] public uint _pad7;
        [FieldOffset(56)] public uint _pad8;
        [FieldOffset(60)] public uint _pad9;
        [FieldOffset(64)] public uint DroppedWrites;
        [FieldOffset(68)] public uint ProducerFlags;
        [FieldOffset(72)] public uint _pad10;
        [FieldOffset(76)] public uint _pad11;
        [FieldOffset(80)] public uint _pad12;
        [FieldOffset(84)] public uint _pad13;
        [FieldOffset(88)] public uint _pad14;
        [FieldOffset(92)] public uint _pad15;
        [FieldOffset(96)] public uint _pad16;
        [FieldOffset(100)] public uint _pad17;
        [FieldOffset(104)] public uint _pad18;
        [FieldOffset(108)] public uint _pad19;
        [FieldOffset(112)] public uint _pad20;
        [FieldOffset(116)] public uint _pad21;
        [FieldOffset(120)] public uint _pad22;
        [FieldOffset(124)] public uint _pad23;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct Shinobu38FileWriterCursorDTO
    {
        [FieldOffset(0)] public int Running;
        [FieldOffset(4)] public int Head;
        [FieldOffset(8)] public uint _pad0;
        [FieldOffset(12)] public uint _pad1;
        [FieldOffset(16)] public uint _pad2;
        [FieldOffset(20)] public uint _pad3;
        [FieldOffset(24)] public uint _pad4;
        [FieldOffset(28)] public uint _pad5;
        [FieldOffset(32)] public uint _pad6;
        [FieldOffset(36)] public uint _pad7;
        [FieldOffset(40)] public uint _pad8;
        [FieldOffset(44)] public uint _pad9;
        [FieldOffset(48)] public uint _pad10;
        [FieldOffset(52)] public uint _pad11;
        [FieldOffset(56)] public uint _pad12;
        [FieldOffset(60)] public uint _pad13;
        [FieldOffset(64)] public int Tail;
        [FieldOffset(68)] public uint _pad14;
        [FieldOffset(72)] public uint _pad15;
        [FieldOffset(76)] public uint _pad16;
        [FieldOffset(80)] public uint _pad17;
        [FieldOffset(84)] public uint _pad18;
        [FieldOffset(88)] public uint _pad19;
        [FieldOffset(92)] public uint _pad20;
        [FieldOffset(96)] public uint _pad21;
        [FieldOffset(100)] public uint _pad22;
        [FieldOffset(104)] public uint _pad23;
        [FieldOffset(108)] public uint _pad24;
        [FieldOffset(112)] public uint _pad25;
        [FieldOffset(116)] public uint _pad26;
        [FieldOffset(120)] public uint _pad27;
        [FieldOffset(124)] public uint _pad28;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct Shinobu38WaypointIngestStateDTO
    {
        [FieldOffset(0)] public long LastSeenTicks;
        [FieldOffset(8)] public int PendingLength;
        [FieldOffset(12)] public int PublishedVersion;
        [FieldOffset(16)] public uint ProducerFlags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public uint _pad1;
        [FieldOffset(28)] public uint _pad2;
        [FieldOffset(32)] public uint _pad3;
        [FieldOffset(36)] public uint _pad4;
        [FieldOffset(40)] public uint _pad5;
        [FieldOffset(44)] public uint _pad6;
        [FieldOffset(48)] public uint _pad7;
        [FieldOffset(52)] public uint _pad8;
        [FieldOffset(56)] public uint _pad9;
        [FieldOffset(60)] public uint _pad10;
        [FieldOffset(64)] public int AppliedVersion;
        [FieldOffset(68)] public int AppliedCount;
        [FieldOffset(72)] public uint ConsumerFlags;
        [FieldOffset(76)] public uint _pad12;
        [FieldOffset(80)] public uint _pad13;
        [FieldOffset(84)] public uint _pad14;
        [FieldOffset(88)] public uint _pad15;
        [FieldOffset(92)] public uint _pad16;
        [FieldOffset(96)] public uint _pad17;
        [FieldOffset(100)] public uint _pad18;
        [FieldOffset(104)] public uint _pad19;
        [FieldOffset(108)] public uint _pad20;
        [FieldOffset(112)] public uint _pad21;
        [FieldOffset(116)] public uint _pad22;
        [FieldOffset(120)] public uint _pad23;
        [FieldOffset(124)] public uint _pad24;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9200)]
    public sealed class Shinobu38QaWatchdogRuntime : MonoBehaviour, IFastTickable, IColdTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001Shinobu38QaWatchdogRuntimeSignalPushDropCount;
        private const string AgentId = "SHINOBU_79";
        private const string RuntimeRootName = "[SHINOBU_79_QA_WATCHDOG]";
        private const string EnvironmentFlagName = "H8_QA_ENDURANCE_10KM";
        private const string FlagRelativePath = "Temp/H8_QA_ENDURANCE_10KM.flag";
        private const string WaypointCsvRelativePath = "Docs/AgentLogs/qa_bot_waypoints.csv";
        private const string CsvRelativePath = "Docs/AgentLogs/QA_Endurance_Report.csv";
        private const string ResultRelativePath = "Docs/AgentLogs/SHINOBU_79_QA_Endurance_Result.json";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_79.bin";
        private const string DumpH8RelativePath = "Docs/AgentLogs/Dump_QA_WATCHDOG.bin";
        private const int RouteCapacity = 16;
        private const int CsvOverrideBytes = 8192;
        private const int CsvScratchBytes = 1024;
        private const int FileWriteQueueCapacity = 128;
        private const int FileWriteQueueMask = FileWriteQueueCapacity - 1;
        private const int FileWritePayloadBytes = 16384;
        private const int FileWritePayloadTotalBytes = FileWriteQueueCapacity * FileWritePayloadBytes;
        private const int TelemetryCapacity = 300;
        private const int InputBufferCapacity = 1;
        private const int RecorderCapacity = 1;
        private const int CrashDumpEntrySizeBytes = 64;
        private const int CrashDumpHeaderSizeBytes = 16;
        private const int CrashDumpBytes = CrashDumpHeaderSizeBytes + (TelemetryCapacity * CrashDumpEntrySizeBytes);
        private const float DefaultTargetDistanceMeters = 10000f;
        private const float DefaultSwimSpeed = 85f;
        private const float DefaultAvoidanceStrength = 1.35f;
        private const float DefaultTelemetryHz = 4f;
        private const float DefaultFastForward = 100f;
        private const float StartupTimeoutSeconds = 90f;
        private const float FatalLowFpsThreshold = 10f;
        private const float FatalLowFpsSeconds = 5f;
        private const float MemoryLeakWindowSeconds = 300f;
        private const float MemoryLeakSlopeBytes = 1024f * 1024f;
        private const float QualityClampWeight = 0.1f;
        private const float QualityClampSeconds = 300f;
        private const float QualityReleaseRampSeconds = 60f;
        private const float MinimumQualityAuditSeconds = QualityClampSeconds + QualityReleaseRampSeconds;
        private const float QualityCycleSeconds = 600f;
        private const float QualityEpsilon = 0.0005f;
        private const float HealthStressPulseSeconds = 10f;
        private const float HealthStressCycleSeconds = 60f;
        private const float CatastrophicAupDeltaMeters = 500f;
        private const float AupJitterFailureMeters = 0.001f;
        private const float RichNormalFadeStart01 = 0.3f;
        private const long BytesPerMegabyte = 1024L * 1024L;
        private const uint KccAupMaxFrameAge = 2u;
        private const uint InputMaskSprint = 1u << 4;
        private const uint InputMaskPrimaryFire = 1u << 2;
        private const uint InputMaskAutomation = 1u << 31;
        private const uint VaultFlagMemoryLeakDetected = 1u;
        private const uint VaultFlagSurvivalPressureEmergency = 1u << 1;
        private const uint VaultFlagFatal = 1u << 2;
        private const uint VaultFlagCsvSlow = 1u << 3;
        private const uint VaultFlagStressRecoveryObserved = 1u << 4;
        private const uint VaultFlagActualAupSampled = 1u << 5;
        private const uint VaultFlagActualAupJitter = 1u << 6;
        private const uint TelemetryFlagAvoidance = 1u;
        private const uint TelemetryFlagRebase = 1u << 1;
        private const uint TelemetryFlagCsvSlow = 1u << 2;
        private const uint TelemetryFlagFatal = 1u << 3;
        private const uint TelemetryFlagStressRecovered = 1u << 4;
        private const uint TelemetryFlagActualAupJitter = 1u << 5;
        private const uint ResultStatusComplete = 0u;
        private const uint ResultStatusFault = 1u;
        private const uint ResultStatusTimeout = 2u;
        private const uint DumpMagic = 0x53373944u;
        private const uint SourceHash = 0x53373951u;
        private const uint EventHashComplete = 0x444F4E45u;
        private const uint EventHashCrash = 0x43525348u;
        private const uint EventHashTimeout = 0x54494D45u;
        private const uint FileTargetCsv = 1u;
        private const uint FileTargetDump = 2u;
        private const uint FileTargetResult = 3u;
        private const uint FileWriterFlagOverflow = 1u;
        private const uint FileWriterFlagException = 1u << 1;
        private const uint WaypointIngestFlagReadError = 1u;
        private const BufferID StateBufferId = (BufferID)70580;
        private const BufferID SnapshotBufferId = (BufferID)70581;
        private const BufferID WaypointsBufferId = (BufferID)70582;
        private const BufferID RebaseSignalsBufferId = (BufferID)70583;
        private const BufferID TuningBufferId = (BufferID)70584;
        private const BufferID MockVaultBufferId = (BufferID)70585;
        private const BufferID TelemetryRingBufferId = (BufferID)70586;
        private const BufferID CsvScratchBufferId = (BufferID)70587;
        private const BufferID WaypointScratchBufferId = (BufferID)70588;
        private const BufferID DumpScratchBufferId = (BufferID)70589;
        private const BufferID FileWriteCommandsBufferId = (BufferID)70590;
        private const BufferID FileWritePayloadBufferId = (BufferID)70591;
        private const BufferID FileWriterStateBufferId = (BufferID)70592;
        private const BufferID FileWriterCursorBufferId = (BufferID)70593;
        private const BufferID WaypointIngestStateBufferId = (BufferID)70594;
        private const SystemID OwnerSystemId = SystemID.External;
        private static readonly ulong RuntimeBufferMutationGuardMask =
            WatchdogMutationGuardBit(StateBufferId) |
            WatchdogMutationGuardBit(SnapshotBufferId) |
            WatchdogMutationGuardBit(BufferID.ShinobuInputCurrentDto) |
            WatchdogMutationGuardBit(WaypointsBufferId) |
            WatchdogMutationGuardBit(RebaseSignalsBufferId) |
            WatchdogMutationGuardBit(TuningBufferId) |
            WatchdogMutationGuardBit(MockVaultBufferId) |
            WatchdogMutationGuardBit(TelemetryRingBufferId) |
            WatchdogMutationGuardBit(CsvScratchBufferId) |
            WatchdogMutationGuardBit(WaypointScratchBufferId) |
            WatchdogMutationGuardBit(DumpScratchBufferId) |
            WatchdogMutationGuardBit(FileWriteCommandsBufferId) |
            WatchdogMutationGuardBit(FileWritePayloadBufferId) |
            WatchdogMutationGuardBit(FileWriterStateBufferId) |
            WatchdogMutationGuardBit(FileWriterCursorBufferId) |
            WatchdogMutationGuardBit(WaypointIngestStateBufferId);

        private static Shinobu38QaWatchdogRuntime _instance;
        private static bool _autoCreated;
        private static Shinobu38TuningDTO _pendingTuning = new Shinobu38TuningDTO
        {
            SwimSpeed = DefaultSwimSpeed,
            ObstacleAvoidanceStrength = DefaultAvoidanceStrength,
            TelemetryWriteFrequency = DefaultTelemetryHz,
            FastForwardScale = DefaultFastForward,
            Tier = (uint)Shinobu38QaTier.Low
        };

        private IDataVault _dataVault;
        private IDataVault _runtimeBufferGuardVault;
        private VaultGenerationHandle<WatchdogStateDTO> _stateHandle;
        private VaultGenerationHandle<TelemetrySnapshotDTO> _snapshotHandle;
        private VaultGenerationHandle<InputStateDTO> _agent36InputHandle;
        private VaultGenerationHandle<Shinobu38RouteWaypointDTO> _waypointsHandle;
        private VaultGenerationHandle<MockRebaseSignal> _mockRebaseSignalsHandle;
        private VaultGenerationHandle<Shinobu38TuningDTO> _tuningHandle;
        private VaultGenerationHandle<Shinobu38MockVaultDTO> _mockVaultHandle;
        private VaultGenerationHandle<Shinobu38WatchdogTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<byte> _waypointScratchHandle;
        private VaultGenerationHandle<byte> _dumpScratchHandle;
        private VaultGenerationHandle<Shinobu38FileWriteCommand> _fileWriteCommandsHandle;
        private VaultGenerationHandle<byte> _fileWritePayloadHandle;
        private VaultGenerationHandle<Shinobu38FileWriterStateDTO> _fileWriterStateHandle;
        private VaultGenerationHandle<Shinobu38FileWriterCursorDTO> _fileWriterCursorHandle;
        private VaultGenerationHandle<Shinobu38WaypointIngestStateDTO> _waypointIngestStateHandle;
        private JobHandle _navigationHandle;
        private ProfilerRecorder _gcUsedRecorder;
        private ProfilerRecorder _totalReservedRecorder;
        private ProfilerRecorder _gfxUsedRecorder;
        private string _csvPath;
        private string _resultPath;
        private string _dumpPath;
        private string _dumpH8Path;
        private string _waypointCsvPath;
        private Thread _fileWriterThread;
        private ManualResetEventSlim _fileWriterEvent;
        private double _startupTime;
        private long _lastWaypointCsvTicks;
        private long _fileWriteSequence;
        private long _nextWaypointPollTicks;
        private long _baselineGcUsedBytes;
        private long _baselineReservedBytes;
        private long _memoryWindowStartBytes;
        private long _qualityClockTicks;
        private long _lastGcUsedBytes;
        private long _lastReservedBytes;
        private long _lastGfxUsedBytes;
        private int _writerLastCsvMicros;
        private int _writerLastAnyMicros;
        private int _writerCompletedWrites;
        private int _writerFaultFlags;
        private float _targetDistanceMeters = DefaultTargetDistanceMeters;
        private float _nextCsvTime;
        private float _memoryWindowElapsed;
        private float _memoryWindowStartWallSeconds;
        private float _lowFpsElapsed;
        private float _lastDistanceForStuck;
        private float _stuckElapsed;
        private float _lastAvoidanceCorrections;
        private float _lastCsvWriteMs;
        private float _qualityWallSeconds;
        private uint _frame;
        private uint _rebaseCount;
        private uint _catastrophicAupDeltaFrame;
        private uint _telemetryCursor;
        private uint _telemetryCount;
        private uint _routeCount;
        private uint _lastEventHash;
        private uint _hardwareFlags;
        private uint _shiftFrameId;
        private ulong _runtimeBufferGuardMask;
        private bool _started;
        private bool _finished;
        private bool _registeredFast;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _navigationPending;
        private bool _vaultBuffersLocked;
        private bool _runtimePolicyCaptured;
        private bool _qualityOverrideActive;
        private bool _hasLastAupAuditPosition;
        private bool _healthStressWasActive;
        private bool _previousRunInBackground;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private float _lastForcedQualityWeight = 1f;
        private double3 _lastAupAuditPosition;

        public static Shinobu38QaWatchdogRuntime Active => _instance;

        public bool IsRunning => _started && !_finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _autoCreated = false;
            _pendingTuning.SwimSpeed = DefaultSwimSpeed;
            _pendingTuning.ObstacleAvoidanceStrength = DefaultAvoidanceStrength;
            _pendingTuning.TelemetryWriteFrequency = DefaultTelemetryHz;
            _pendingTuning.FastForwardScale = DefaultFastForward;
            _pendingTuning.Tier = (uint)Shinobu38QaTier.Low;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_autoCreated || _instance != null || !ShouldRunStatic())
                return;

            _autoCreated = true;
            GameObject root = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - centralized QA watchdog bootstrap root - owner: Shinobu38QaWatchdogRuntime
            root.SetActive(false);
            _instance = root.AddComponent<Shinobu38QaWatchdogRuntime>(); // COLD ALLOC: Shinobu38QaWatchdogRuntime[1] - headless watchdog runtime facade - owner: Shinobu38QaWatchdogRuntime
            Object.DontDestroyOnLoad(root);
            root.SetActive(true);
        }

        public static bool TryWriteTuning(float swimSpeed, float avoidanceStrength, float telemetryHz)
        {
            _pendingTuning.SwimSpeed = math.max(0.1f, swimSpeed);
            _pendingTuning.ObstacleAvoidanceStrength = math.max(0f, avoidanceStrength);
            _pendingTuning.TelemetryWriteFrequency = math.max(0.1f, telemetryHz);
            Shinobu38QaWatchdogRuntime active = _instance;
            if (active == null ||
                !TryResolveWatchdogVaultBuffer(
                    active._dataVault,
                    in active._tuningHandle,
                    TuningBufferId,
                    1,
                    out NativeArray<Shinobu38TuningDTO> tuningBuffer))
            {
                return false;
            }

            ref Shinobu38TuningDTO tuning = ref ElementRef(tuningBuffer, 0);
            tuning.SwimSpeed = _pendingTuning.SwimSpeed;
            tuning.ObstacleAvoidanceStrength = _pendingTuning.ObstacleAvoidanceStrength;
            tuning.TelemetryWriteFrequency = _pendingTuning.TelemetryWriteFrequency;
            return true;
        }

        public static bool TryGetDebugPath(out double3 current, out double3 target, out float3 avoidanceNormal)
        {
            current = double3.zero;
            target = double3.zero;
            avoidanceNormal = float3.zero;
            Shinobu38QaWatchdogRuntime active = _instance;
            if (active == null ||
                !TryReadWatchdogVaultBuffer(
                    active._dataVault,
                    in active._stateHandle,
                    StateBufferId,
                    1,
                    out NativeArray<WatchdogStateDTO> stateBuffer) ||
                !TryReadWatchdogVaultBuffer(
                    active._dataVault,
                    in active._mockVaultHandle,
                    MockVaultBufferId,
                    1,
                    out NativeArray<Shinobu38MockVaultDTO> mockVault))
            {
                return false;
            }

            Shinobu38MockVaultDTO vault = mockVault[0];
            WatchdogStateDTO state = stateBuffer[0];
            current = vault.CurrentAUP;
            target = state.CurrentTargetAUP;
            double3 localToTarget = current - target;
            avoidanceNormal = Shinobu38MockTerrainSdf.SampleNormal(new float3((float)localToTarget.x, (float)localToTarget.y, (float)localToTarget.z));
            return true;
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
            Object.DontDestroyOnLoad(gameObject);
            _ = RunStartupAsync(destroyCancellationToken);
        }

        private async Awaitable RunStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                _startupTime = Time.realtimeSinceStartupAsDouble;
                while ((GlobalRegistry.DataVault == null || GlobalRegistry.Dispatcher == null) &&
                       Time.realtimeSinceStartupAsDouble - _startupTime < StartupTimeoutSeconds)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                }

                if (GlobalRegistry.DataVault == null || GlobalRegistry.Dispatcher == null)
                {
                    Finish(ResultStatusTimeout, EventHashTimeout);
                    return;
                }

                InitializeColdState();
                RegisterRuntime();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                Finish(ResultStatusFault, EventHashCrash);
            }
        }

        private void OnDestroy()
        {
            if (_navigationPending)
            {
                if (DispatcherJobFence.TryComplete(ref _navigationHandle, forceComplete: true))
                    _navigationPending = false;
            }

            StopFileWriter(flushPending: true);
            UnregisterRuntime();
            ReleaseQualityWeightOverride();
            RestoreRuntimePolicy();
            DisposeRecorders();
            UnlockRuntimeBuffers();
            ReleaseWatchdogVaultHandles(_dataVault);
            if (_instance == this)
                _instance = null;
        }

        public void FastTick(float deltaTime)
        {
            if (!_started || _finished || _navigationPending)
                return;

            float safeDelta = math.isfinite(deltaTime) && deltaTime > 0f ? math.min(deltaTime, 0.25f) : 0f;
            if (safeDelta <= 0f)
                return;

            SampleMemoryRecorders();
            SampleQualityWallClock();
            float forcedQualityWeight = ApplyQualityWeightModulation(_qualityWallSeconds);
            IDataVault vault = _dataVault;
            if (!TryResolveWatchdogVaultBuffer(vault, in _stateHandle, StateBufferId, 1, out NativeArray<WatchdogStateDTO> stateBuffer) ||
                !TryResolveWatchdogVaultBuffer(vault, in _snapshotHandle, SnapshotBufferId, 1, out NativeArray<TelemetrySnapshotDTO> snapshotBuffer) ||
                !TryResolveWatchdogVaultBuffer(vault, in _agent36InputHandle, BufferID.ShinobuInputCurrentDto, InputBufferCapacity, out NativeArray<InputStateDTO> inputBuffer) ||
                !TryResolveWatchdogVaultBuffer(vault, in _waypointsHandle, WaypointsBufferId, RouteCapacity, out NativeArray<Shinobu38RouteWaypointDTO> waypoints) ||
                !TryResolveWatchdogVaultBuffer(vault, in _mockRebaseSignalsHandle, RebaseSignalsBufferId, 1, out NativeArray<MockRebaseSignal> rebaseSignals) ||
                !TryResolveWatchdogVaultBuffer(vault, in _tuningHandle, TuningBufferId, 1, out NativeArray<Shinobu38TuningDTO> tuningBuffer) ||
                !TryResolveWatchdogVaultBuffer(vault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault))
            {
                Finish(ResultStatusFault, EventHashCrash);
                return;
            }

            Shinobu38TuningDTO tuning = tuningBuffer[0];
            float scaledDelta = safeDelta * math.max(1f, tuning.FastForwardScale);
            _frame++;

            BotNavigationJob job = new BotNavigationJob
            {
                State = stateBuffer,
                Snapshot = snapshotBuffer,
                InputBuffer = inputBuffer,
                Waypoints = waypoints,
                RebaseSignals = rebaseSignals,
                Tuning = tuningBuffer,
                MockVault = mockVault,
                DeltaTime = scaledDelta,
                WallDeltaTime = safeDelta,
                Frame = _frame,
                RouteCount = _routeCount,
                TargetDistanceMeters = _targetDistanceMeters,
                BaseGcBytes = _baselineGcUsedBytes,
                CurrentGcBytes = _lastGcUsedBytes,
                CurrentVramBytes = _lastGfxUsedBytes,
                SystemHealthIndex01 = ResolveSystemHealthIndex01(),
                QualityWeight = forcedQualityWeight
            };
            _navigationHandle = job.Schedule();
            H8Memory.RegisterActiveJob(SystemID.External, _navigationHandle);
            _navigationPending = true;
        }

        public void LateFrameTick()
        {
            if (!_started || _finished)
                return;

            if (!_navigationPending)
                return;

            if (!DispatcherJobFence.TryComplete(ref _navigationHandle, forceComplete: Application.isBatchMode))
                return;

            _navigationPending = false;
            ConsumeNavigationResult();
        }

        public void ColdTick()
        {
            if (!_started || _finished)
                return;

            ApplyWaypointCsvOverrideIfReady();
        }

        private void InitializeColdState()
        {
            ResolveRuntimePaths();
            EnsureParentDirectory(_csvPath);
            EnsureParentDirectory(_resultPath);
            EnsureParentDirectory(_dumpPath);
            EnsureParentDirectory(_dumpH8Path);
            TryDeleteFile(_resultPath);
            TryDeleteFile(_resultPath + ".tmp");
            TryDeleteFile(_dumpPath);
            TryDeleteFile(_dumpH8Path);
            TryDeleteFile(_csvPath);

            _targetDistanceMeters = math.max(1f, TryReadFloatArg("-h8qaDistanceMeters", DefaultTargetDistanceMeters));
            _hardwareFlags = ResolveHardwareFlags();
            Shinobu38TuningDTO tuning = _pendingTuning;
            tuning.SwimSpeed = math.max(0.1f, TryReadFloatArg("-h8qaSpeed", tuning.SwimSpeed));
            tuning.FastForwardScale = math.max(1f, TryReadFloatArg("-h8qaFastForward", tuning.FastForwardScale));
            tuning.TelemetryWriteFrequency = math.max(0.1f, TryReadFloatArg("-h8qaTelemetryHz", tuning.TelemetryWriteFrequency));
            tuning.ObstacleAvoidanceStrength = math.max(0f, TryReadFloatArg("-h8qaAvoidance", tuning.ObstacleAvoidanceStrength));
            tuning.Tier = (uint)ResolveTierFromArgs();

            _dataVault = GlobalRegistry.DataVault;
            ResolveVaultHandles();
            if (!LockRuntimeBuffers())
                throw new InvalidOperationException("SHINOBU_79 failed to lock DataVault buffers.");

            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _stateHandle, StateBufferId, 1, out NativeArray<WatchdogStateDTO> stateBuffer) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _snapshotHandle, SnapshotBufferId, 1, out NativeArray<TelemetrySnapshotDTO> snapshotBuffer) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _agent36InputHandle, BufferID.ShinobuInputCurrentDto, InputBufferCapacity, out NativeArray<InputStateDTO> inputBuffer) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _waypointsHandle, WaypointsBufferId, RouteCapacity, out NativeArray<Shinobu38RouteWaypointDTO> waypoints) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _mockRebaseSignalsHandle, RebaseSignalsBufferId, 1, out NativeArray<MockRebaseSignal> rebaseSignals) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _tuningHandle, TuningBufferId, 1, out NativeArray<Shinobu38TuningDTO> tuningBuffer) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _telemetryRingHandle, TelemetryRingBufferId, TelemetryCapacity, out NativeArray<Shinobu38WatchdogTelemetryEntry> telemetryRing) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _csvScratchHandle, CsvScratchBufferId, CsvScratchBytes, out NativeArray<byte> csvScratch) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _waypointScratchHandle, WaypointScratchBufferId, CsvOverrideBytes, out NativeArray<byte> waypointScratch) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _dumpScratchHandle, DumpScratchBufferId, CrashDumpBytes, out NativeArray<byte> dumpScratch) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriteCommandsHandle, FileWriteCommandsBufferId, FileWriteQueueCapacity, out NativeArray<Shinobu38FileWriteCommand> fileCommands) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWritePayloadHandle, FileWritePayloadBufferId, FileWritePayloadTotalBytes, out NativeArray<byte> filePayload) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterStateHandle, FileWriterStateBufferId, 1, out NativeArray<Shinobu38FileWriterStateDTO> fileWriterState) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterCursorHandle, FileWriterCursorBufferId, 1, out NativeArray<Shinobu38FileWriterCursorDTO> fileWriterCursor) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _waypointIngestStateHandle, WaypointIngestStateBufferId, 1, out NativeArray<Shinobu38WaypointIngestStateDTO> waypointIngestState))
            {
                throw new InvalidOperationException("SHINOBU_79 failed to resolve DataVault buffers.");
            }

            JobHandle clearHandle = default;
            clearHandle = ScheduleClear(stateBuffer, clearHandle);
            clearHandle = ScheduleClear(snapshotBuffer, clearHandle);
            clearHandle = ScheduleClear(inputBuffer, clearHandle);
            clearHandle = ScheduleClear(waypoints, clearHandle);
            clearHandle = ScheduleClear(rebaseSignals, clearHandle);
            clearHandle = ScheduleClear(tuningBuffer, clearHandle);
            clearHandle = ScheduleClear(mockVault, clearHandle);
            clearHandle = ScheduleClear(telemetryRing, clearHandle);
            clearHandle = ScheduleClear(csvScratch, clearHandle);
            clearHandle = ScheduleClear(waypointScratch, clearHandle);
            clearHandle = ScheduleClear(dumpScratch, clearHandle);
            clearHandle = ScheduleClear(fileCommands, clearHandle);
            clearHandle = ScheduleClear(filePayload, clearHandle);
            clearHandle = ScheduleClear(fileWriterState, clearHandle);
            clearHandle = ScheduleClear(fileWriterCursor, clearHandle);
            clearHandle = ScheduleClear(waypointIngestState, clearHandle);
            DispatcherJobFence.TryComplete(ref clearHandle, forceComplete: true); // COLD SYNC JOB: init-only MemClear before first watchdog frame - owner: Shinobu38QaWatchdogRuntime

            tuningBuffer[0] = tuning;
            GenerateEmergencyMockRoute(stateBuffer, waypoints, mockVault);
            StartFileWriter();
            QueueCsvHeader(csvScratch);
            StartRecorders();
            _baselineGcUsedBytes = ReadRecorderBytes(_gcUsedRecorder, Profiler.GetMonoUsedSizeLong());
            _baselineReservedBytes = ReadRecorderBytes(_totalReservedRecorder, Profiler.GetTotalReservedMemoryLong());
            _memoryWindowStartBytes = _baselineReservedBytes;
            _memoryWindowStartWallSeconds = 0f;
            _nextCsvTime = 0f;
            _qualityWallSeconds = 0f;
            _qualityClockTicks = Stopwatch.GetTimestamp();
            _catastrophicAupDeltaFrame = 0u;
            _hasLastAupAuditPosition = false;
            _healthStressWasActive = false;
            _lastAupAuditPosition = double3.zero;
        }

        private void ResolveVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                throw new InvalidOperationException("SHINOBU_79 requires GlobalRegistry.DataVault.");

            _stateHandle = vault.EnsureGenerationHandle<WatchdogStateDTO>(StateBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _snapshotHandle = vault.EnsureGenerationHandle<TelemetrySnapshotDTO>(SnapshotBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _agent36InputHandle = vault.EnsureGenerationHandle<InputStateDTO>(BufferID.ShinobuInputCurrentDto, InputBufferCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _waypointsHandle = vault.EnsureGenerationHandle<Shinobu38RouteWaypointDTO>(WaypointsBufferId, RouteCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _mockRebaseSignalsHandle = vault.EnsureGenerationHandle<MockRebaseSignal>(RebaseSignalsBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<Shinobu38TuningDTO>(TuningBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _mockVaultHandle = vault.EnsureGenerationHandle<Shinobu38MockVaultDTO>(MockVaultBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<Shinobu38WatchdogTelemetryEntry>(TelemetryRingBufferId, TelemetryCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(CsvScratchBufferId, CsvScratchBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _waypointScratchHandle = vault.EnsureGenerationHandle<byte>(WaypointScratchBufferId, CsvOverrideBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _dumpScratchHandle = vault.EnsureGenerationHandle<byte>(DumpScratchBufferId, CrashDumpBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _fileWriteCommandsHandle = vault.EnsureGenerationHandle<Shinobu38FileWriteCommand>(FileWriteCommandsBufferId, FileWriteQueueCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _fileWritePayloadHandle = vault.EnsureGenerationHandle<byte>(FileWritePayloadBufferId, FileWritePayloadTotalBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _fileWriterStateHandle = vault.EnsureGenerationHandle<Shinobu38FileWriterStateDTO>(FileWriterStateBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _fileWriterCursorHandle = vault.EnsureGenerationHandle<Shinobu38FileWriterCursorDTO>(FileWriterCursorBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _waypointIngestStateHandle = vault.EnsureGenerationHandle<Shinobu38WaypointIngestStateDTO>(WaypointIngestStateBufferId, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
        }

        private static bool TryResolveWatchdogVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsWatchdogVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadWatchdogVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsWatchdogVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsWatchdogVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static unsafe ref T ElementRef<T>(NativeArray<T> buffer, int index) where T : struct
        {
            if (!buffer.IsCreated || (uint)index >= (uint)buffer.Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            void* ptr = (byte*)buffer.GetUnsafePtr() + (index * UnsafeUtility.SizeOf<T>());
            return ref UnsafeUtility.AsRef<T>(ptr);
        }

        private bool LockRuntimeBuffers()
        {
            if (_vaultBuffersLocked)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryAcquireMutationGuard(RuntimeBufferMutationGuardMask))
                return false;

            _runtimeBufferGuardVault = vault;
            _runtimeBufferGuardMask = RuntimeBufferMutationGuardMask;
            _vaultBuffersLocked = true;
            return true;
        }

        private void UnlockRuntimeBuffers()
        {
            if (!_vaultBuffersLocked)
                return;

            IDataVault vault = _runtimeBufferGuardVault ?? _dataVault;
            ulong guardMask = _runtimeBufferGuardMask;
            _runtimeBufferGuardVault = null;
            _runtimeBufferGuardMask = 0UL;
            _vaultBuffersLocked = false;
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static ulong WatchdogMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private void ReleaseWatchdogVaultHandles(IDataVault vault)
        {
            ReleaseWatchdogVaultHandle(vault, ref _stateHandle, StateBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _snapshotHandle, SnapshotBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _agent36InputHandle, BufferID.ShinobuInputCurrentDto);
            ReleaseWatchdogVaultHandle(vault, ref _waypointsHandle, WaypointsBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _mockRebaseSignalsHandle, RebaseSignalsBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _tuningHandle, TuningBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _mockVaultHandle, MockVaultBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _telemetryRingHandle, TelemetryRingBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _csvScratchHandle, CsvScratchBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _waypointScratchHandle, WaypointScratchBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _dumpScratchHandle, DumpScratchBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _fileWriteCommandsHandle, FileWriteCommandsBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _fileWritePayloadHandle, FileWritePayloadBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _fileWriterStateHandle, FileWriterStateBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _fileWriterCursorHandle, FileWriterCursorBufferId);
            ReleaseWatchdogVaultHandle(vault, ref _waypointIngestStateHandle, WaypointIngestStateBufferId);
        }

        private static void ReleaseWatchdogVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsWatchdogVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void RegisterRuntime()
        {
            ForceRuntimePolicy();
            ApplyQualityWeightModulation(0f);
            TryRegisterHotSwapListener();
            RegisterRuntimeLanes();
            if (!_started)
                Finish(ResultStatusFault, EventHashCrash);
        }

        private void RegisterRuntimeLanes()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_registeredFast || _registeredCold || _registeredLate)
                UnregisterRuntimeLanes();

            _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Core);
            _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            _started = _registeredFast && _registeredCold && _registeredLate;
            if (!_started)
                UnregisterRuntimeLanes();
        }

        private void UnregisterRuntime()
        {
            TryUnregisterHotSwapListener();
            UnregisterRuntimeLanes();
            UnlockRuntimeBuffers();
        }

        private void UnregisterRuntimeLanes()
        {
            if (_registeredFast)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Core);
                _registeredFast = false;
            }

            if (_registeredCold)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Core);
                _registeredCold = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLate = false;
            }

            _started = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            _registeredFast = false;
            _registeredCold = false;
            _registeredLate = false;
            _started = false;

            if (currentService == null ||
                _finished ||
                !isActiveAndEnabled)
            {
                return;
            }

            RegisterRuntimeLanes();
            if (!_started)
                Finish(ResultStatusFault, EventHashCrash);
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

        private void ConsumeNavigationResult()
        {
            IDataVault dataVault = _dataVault;
            if (!TryResolveWatchdogVaultBuffer(dataVault, in _stateHandle, StateBufferId, 1, out NativeArray<WatchdogStateDTO> stateBuffer) ||
                !TryResolveWatchdogVaultBuffer(dataVault, in _snapshotHandle, SnapshotBufferId, 1, out NativeArray<TelemetrySnapshotDTO> snapshotBuffer) ||
                !TryResolveWatchdogVaultBuffer(dataVault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault) ||
                !TryResolveWatchdogVaultBuffer(dataVault, in _mockRebaseSignalsHandle, RebaseSignalsBufferId, 1, out NativeArray<MockRebaseSignal> rebaseSignals) ||
                !TryResolveWatchdogVaultBuffer(dataVault, in _tuningHandle, TuningBufferId, 1, out NativeArray<Shinobu38TuningDTO> tuningBuffer))
            {
                Finish(ResultStatusFault, EventHashCrash);
                return;
            }

            WatchdogStateDTO state = stateBuffer[0];
            TelemetrySnapshotDTO snapshot = snapshotBuffer[0];
            Shinobu38MockVaultDTO vault = mockVault[0];
            MockRebaseSignal rebase = rebaseSignals[0];
            SampleFileWriterTelemetry();
            PublishAutomationInputOverride();
            AuditCatastrophicAupDelta(ref state, ref vault, in rebase);
            AuditActualKccAupJitter(ref state, ref snapshot, ref vault);
            stateBuffer[0] = state;
            snapshotBuffer[0] = snapshot;
            mockVault[0] = vault;
            float forcedQualityWeight = _lastForcedQualityWeight;
            if (rebase.Frame == _frame && (rebase.Flags & 1u) != 0u)
            {
                _rebaseCount++;
                _shiftFrameId++;
            }

            _lastAvoidanceCorrections = (vault.FrameFlags & TelemetryFlagAvoidance) != 0u ? 1f : 0f;
            _memoryWindowElapsed = math.max(0f, _qualityWallSeconds - _memoryWindowStartWallSeconds);
            if (snapshot.FrameTimeMs > (1000f / FatalLowFpsThreshold))
                _lowFpsElapsed += snapshot.FrameTimeMs * 0.001f;
            else
                _lowFpsElapsed = 0f;

            float distanceDelta = state.DistanceTraveled - _lastDistanceForStuck;
            if (distanceDelta < 0.05f)
                _stuckElapsed += snapshot.FrameTimeMs * 0.001f;
            else
                _stuckElapsed = 0f;
            _lastDistanceForStuck = state.DistanceTraveled;

            if (_memoryWindowElapsed >= MemoryLeakWindowSeconds)
            {
                long delta = _lastReservedBytes - _memoryWindowStartBytes;
                if (delta > MemoryLeakSlopeBytes)
                {
                    vault.Flags |= VaultFlagMemoryLeakDetected;
                    mockVault[0] = vault;
                }

                _memoryWindowStartBytes = _lastReservedBytes;
                _memoryWindowStartWallSeconds = _qualityWallSeconds;
                _memoryWindowElapsed = 0f;
            }

            PublishSystemHealthStress();
            uint flags = BuildTelemetryFlags(in vault);
            RecordTelemetry(in state, in vault, flags);

            if (state.TestDuration >= _nextCsvTime)
            {
                if (!TryResolveWatchdogVaultBuffer(dataVault, in _csvScratchHandle, CsvScratchBufferId, CsvScratchBytes, out NativeArray<byte> csvScratch))
                {
                    Finish(ResultStatusFault, EventHashCrash);
                    return;
                }

                int recordBytes = Shinobu38CsvStreamer.WriteRecord(
                    csvScratch,
                    _frame,
                    state.TestDuration,
                    _qualityWallSeconds,
                    in vault.CurrentAUP,
                    snapshot.FrameTimeMs,
                    _lastGcUsedBytes,
                    _lastReservedBytes,
                    _lastGfxUsedBytes,
                    ResolveSystemHealthIndex01(),
                    forcedQualityWeight,
                    ResolveThermal01(),
                    ResolveIoPressure01(),
                    snapshot.AupJitterError,
                    state.DistanceTraveled,
                    _rebaseCount,
                    _hardwareFlags,
                    vault.Flags);
                bool queued = TryQueueFileWrite(FileTargetCsv, csvScratch, recordBytes);
                _nextCsvTime = state.TestDuration + (1f / math.max(0.1f, tuningBuffer[0].TelemetryWriteFrequency));
                if (!queued || _lastCsvWriteMs > 1f)
                {
                    bool dumpAlreadyQueued = (vault.Flags & VaultFlagCsvSlow) != 0u;
                    vault.Flags |= VaultFlagCsvSlow;
                    mockVault[0] = vault;
                    if (!dumpAlreadyQueued)
                        DumpTelemetry();
                }
            }

            if ((vault.Flags & VaultFlagFatal) != 0u ||
                (vault.Flags & VaultFlagMemoryLeakDetected) != 0u ||
                _lowFpsElapsed >= FatalLowFpsSeconds ||
                _stuckElapsed >= FatalLowFpsSeconds)
            {
                Finish(ResultStatusFault, EventHashCrash);
                return;
            }

            if (ShouldFinishSuccessfully(in state, in vault))
                Finish(ResultStatusComplete, EventHashComplete);
        }

        private bool ShouldFinishSuccessfully(in WatchdogStateDTO state, in Shinobu38MockVaultDTO vault)
        {
            if (state.DistanceTraveled < _targetDistanceMeters)
                return false;

            return HasQualityAuditObserved(vault.Flags);
        }

        private bool HasQualityAuditObserved(uint vaultFlags)
        {
            return _qualityWallSeconds >= MinimumQualityAuditSeconds &&
                   (vaultFlags & VaultFlagStressRecoveryObserved) != 0u;
        }

        private uint BuildTelemetryFlags(in Shinobu38MockVaultDTO vault)
        {
            uint flags = 0u;
            if ((vault.FrameFlags & TelemetryFlagAvoidance) != 0u)
                flags |= TelemetryFlagAvoidance;
            if (_rebaseCount > 0u)
                flags |= TelemetryFlagRebase;
            if (_lastCsvWriteMs > 1f)
                flags |= TelemetryFlagCsvSlow;
            if ((vault.Flags & VaultFlagFatal) != 0u)
                flags |= TelemetryFlagFatal;
            if ((vault.Flags & VaultFlagStressRecoveryObserved) != 0u)
                flags |= TelemetryFlagStressRecovered;
            if ((vault.Flags & VaultFlagActualAupJitter) != 0u)
                flags |= TelemetryFlagActualAupJitter;
            return flags;
        }

        private void RecordTelemetry(in WatchdogStateDTO state, in Shinobu38MockVaultDTO vault, uint flags)
        {
            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _telemetryRingHandle, TelemetryRingBufferId, TelemetryCapacity, out NativeArray<Shinobu38WatchdogTelemetryEntry> telemetryRing))
                return;

            int index = (int)(_telemetryCursor % TelemetryCapacity);
            double3 localAup = vault.CurrentAUP - state.CurrentTargetAUP;
            int localMmX = ClampToInt(localAup.x * 1000d);
            int localMmY = ClampToInt(localAup.y * 1000d);
            int localMmZ = ClampToInt(localAup.z * 1000d);
            int sectorX = ResolveAupSector(vault.CurrentAUP.x);
            int sectorY = ResolveAupSector(vault.CurrentAUP.y);
            int sectorZ = ResolveAupSector(vault.CurrentAUP.z);
            telemetryRing[index] = new Shinobu38WatchdogTelemetryEntry
            {
                Frame = _frame,
                TargetDistanceRemaining = math.max(0f, _targetDistanceMeters - state.DistanceTraveled),
                AvoidanceCorrections = _lastAvoidanceCorrections,
                CsvWriteTimeMs = _lastCsvWriteMs,
                LocalMillimetersX = localMmX,
                LocalMillimetersY = localMmY,
                LocalMillimetersZ = localMmZ,
                Flags = flags,
                SectorX = sectorX,
                SectorY = sectorY,
                SectorZ = sectorZ,
                ShiftFrameId = _shiftFrameId,
                AupHash = HashAupTelemetry(sectorX, sectorY, sectorZ, localMmX, localMmY, localMmZ)
            };
            _telemetryCursor++;
            if (_telemetryCount < TelemetryCapacity)
                _telemetryCount++;
        }

        private void PublishSystemHealthStress()
        {
            float phase = _qualityWallSeconds - math.floor(_qualityWallSeconds / HealthStressCycleSeconds) * HealthStressCycleSeconds;
            bool stressActive = phase < HealthStressPulseSeconds;
            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault))
                return;

            Shinobu38MockVaultDTO vault = mockVault[0];
            if (stressActive)
            {
                SystemHealthIndexSignal signal = new SystemHealthIndexSignal
                {
                    Health01 = 0.95f,
                    Pressure01 = 0.95f,
                    Frame = _frame,
                    SourceHash = SourceHash,
                    State = SystemHealthIndexSignal.StateCritical,
                    Flags = SystemHealthIndexSignal.FlagAdrenaline
                };
                SignalBus<SystemHealthIndexSignal>.TryPushTracked(in signal, ref s_x001Shinobu38QaWatchdogRuntimeSignalPushDropCount);
                vault.Flags |= VaultFlagSurvivalPressureEmergency;
                _healthStressWasActive = true;
                mockVault[0] = vault;
                return;
            }

            if (_healthStressWasActive)
            {
                vault.Flags &= ~VaultFlagSurvivalPressureEmergency;
                vault.Flags |= VaultFlagStressRecoveryObserved;
                vault.FrameFlags |= TelemetryFlagStressRecovered;
                _healthStressWasActive = false;
            }

            mockVault[0] = vault;
        }

        private void AuditActualKccAupJitter(ref WatchdogStateDTO state, ref TelemetrySnapshotDTO snapshot, ref Shinobu38MockVaultDTO vault)
        {
            if (!TryResolveLatestKccAup(out double3 actualAup))
                return;

            vault.Flags |= VaultFlagActualAupSampled;
            double3 intendedAup = vault.CurrentAUP;
            double3 localDelta = actualAup - intendedAup;
            float3 localFloat = new float3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
            if (!math.all(math.isfinite(localFloat)))
            {
                state.ErrorCount++;
                vault.Flags |= VaultFlagFatal | VaultFlagActualAupJitter;
                vault.FrameFlags |= TelemetryFlagFatal | TelemetryFlagActualAupJitter;
                return;
            }

            float positionErrorMeters = math.length(localFloat);
            double3 reconstructed = intendedAup + (double3)localFloat;
            double3 error = actualAup - reconstructed;
            float3 errorLocal = new float3((float)error.x, (float)error.y, (float)error.z);
            if (!math.all(math.isfinite(errorLocal)))
            {
                state.ErrorCount++;
                vault.Flags |= VaultFlagFatal | VaultFlagActualAupJitter;
                vault.FrameFlags |= TelemetryFlagFatal | TelemetryFlagActualAupJitter;
                return;
            }

            float reconstructionErrorMeters = math.length(errorLocal);
            float worstErrorMeters = math.max(positionErrorMeters, reconstructionErrorMeters);
            snapshot.AupJitterError = math.max(snapshot.AupJitterError, worstErrorMeters * 1000f);
            if (worstErrorMeters > AupJitterFailureMeters)
            {
                state.ErrorCount++;
                vault.Flags |= VaultFlagActualAupJitter;
                vault.FrameFlags |= TelemetryFlagActualAupJitter;
            }
        }

        private static bool TryResolveLatestKccAup(out double3 actualAup)
        {
            actualAup = double3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal))
                return false;

            uint engineFrame = SystemDispatcher.CurrentFrameId;
            if (signal.Frame > engineFrame || engineFrame - signal.Frame > KccAupMaxFrameAge)
                return false;

            actualAup = signal.BodyAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(actualAup));
        }

        private void AuditCatastrophicAupDelta(ref WatchdogStateDTO state, ref Shinobu38MockVaultDTO vault, in MockRebaseSignal rebase)
        {
            double3 current = vault.CurrentAUP;
            if (!_hasLastAupAuditPosition)
            {
                _lastAupAuditPosition = current;
                _hasLastAupAuditPosition = true;
                return;
            }

            double3 delta = current - _lastAupAuditPosition;
            float3 localDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            bool rebaseActive = rebase.Frame == _frame && (rebase.Flags & 1u) != 0u;
            if (!rebaseActive && math.all(math.isfinite(localDelta)) && math.length(localDelta) > CatastrophicAupDeltaMeters)
            {
                state.ErrorCount++;
                vault.Flags |= VaultFlagFatal;
                vault.FrameFlags |= TelemetryFlagFatal;
                _catastrophicAupDeltaFrame = _frame;
            }

            _lastAupAuditPosition = current;
        }

        private void Finish(uint status, uint eventHash)
        {
            if (_finished)
                return;

            _finished = true;
            _lastEventHash = eventHash;
            if (status != ResultStatusComplete)
            {
                TryResolveWatchdogVaultBuffer(_dataVault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault);
                Shinobu38MockVaultDTO vault = mockVault.IsCreated ? mockVault[0] : default;
                vault.Flags |= VaultFlagFatal;
                if (mockVault.IsCreated)
                    mockVault[0] = vault;
                DumpTelemetry();
            }

            WriteResult(status, eventHash);
            ReleaseQualityWeightOverride();
            StopFileWriter(flushPending: true);
            UnregisterRuntime();
            if (Application.isBatchMode && !Application.isEditor)
                Application.Quit(status == ResultStatusComplete ? 0 : 1);
        }

        private void DumpTelemetry()
        {
            if (!TryReadWatchdogVaultBuffer(_dataVault, in _telemetryRingHandle, TelemetryRingBufferId, TelemetryCapacity, out NativeArray<Shinobu38WatchdogTelemetryEntry> telemetryRing) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _dumpScratchHandle, DumpScratchBufferId, CrashDumpBytes, out NativeArray<byte> dumpScratch))
            {
                return;
            }

            int cursor = 0;
            WriteUInt(dumpScratch, ref cursor, DumpMagic);
            WriteUInt(dumpScratch, ref cursor, _telemetryCount);
            WriteUInt(dumpScratch, ref cursor, CrashDumpEntrySizeBytes);
            WriteUInt(dumpScratch, ref cursor, _telemetryCursor);
            uint count = math.min(_telemetryCount, (uint)TelemetryCapacity);
            uint start = _telemetryCursor >= count ? _telemetryCursor - count : 0u;
            for (uint i = 0u; i < count; i++)
            {
                int index = (int)((start + i) % TelemetryCapacity);
                Shinobu38WatchdogTelemetryEntry entry = telemetryRing[index];
                WriteUInt(dumpScratch, ref cursor, entry.Frame);
                WriteFloat(dumpScratch, ref cursor, entry.TargetDistanceRemaining);
                WriteFloat(dumpScratch, ref cursor, entry.AvoidanceCorrections);
                WriteFloat(dumpScratch, ref cursor, entry.CsvWriteTimeMs);
                WriteUInt(dumpScratch, ref cursor, unchecked((uint)entry.LocalMillimetersX));
                WriteUInt(dumpScratch, ref cursor, unchecked((uint)entry.LocalMillimetersY));
                WriteUInt(dumpScratch, ref cursor, unchecked((uint)entry.LocalMillimetersZ));
                WriteUInt(dumpScratch, ref cursor, entry.Flags);
                WriteUInt(dumpScratch, ref cursor, unchecked((uint)entry.SectorX));
                WriteUInt(dumpScratch, ref cursor, unchecked((uint)entry.SectorY));
                WriteUInt(dumpScratch, ref cursor, unchecked((uint)entry.SectorZ));
                WriteUInt(dumpScratch, ref cursor, entry.ShiftFrameId);
                WriteUInt(dumpScratch, ref cursor, entry.AupHash);
                WriteUInt(dumpScratch, ref cursor, entry._pad0);
                WriteUInt(dumpScratch, ref cursor, entry._pad1);
                WriteUInt(dumpScratch, ref cursor, entry._pad2);
            }

            TryQueueFileWrite(FileTargetDump, dumpScratch, cursor);
        }

        private void WriteResult(uint status, uint eventHash)
        {
            TryReadWatchdogVaultBuffer(_dataVault, in _stateHandle, StateBufferId, 1, out NativeArray<WatchdogStateDTO> stateBuffer);
            TryReadWatchdogVaultBuffer(_dataVault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault);
            TryResolveWatchdogVaultBuffer(_dataVault, in _dumpScratchHandle, DumpScratchBufferId, CrashDumpBytes, out NativeArray<byte> dumpScratch);
            WatchdogStateDTO state = stateBuffer.IsCreated ? stateBuffer[0] : default;
            Shinobu38MockVaultDTO vault = mockVault.IsCreated ? mockVault[0] : default;
            if (!dumpScratch.IsCreated)
                return;

            Shinobu38AsciiBuffer writer = new Shinobu38AsciiBuffer(dumpScratch);
            writer.AppendAscii("{\"agent\":\"");
            writer.AppendAscii(AgentId);
            writer.AppendAscii("\",\"status\":");
            writer.AppendUInt(status);
            writer.AppendAscii(",\"eventHash\":");
            writer.AppendUInt(eventHash);
            writer.AppendAscii(",\"distanceMeters\":");
            writer.AppendFloat(state.DistanceTraveled);
            writer.AppendAscii(",\"durationSeconds\":");
            writer.AppendFloat(state.TestDuration);
            writer.AppendAscii(",\"errors\":");
            writer.AppendUInt(state.ErrorCount);
            writer.AppendAscii(",\"rebaseCount\":");
            writer.AppendUInt(_rebaseCount);
            writer.AppendAscii(",\"vaultFlags\":");
            writer.AppendUInt(vault.Flags);
            writer.AppendAscii(",\"qualityWeight\":");
            writer.AppendFloat(_lastForcedQualityWeight);
            writer.AppendAscii(",\"wallSeconds\":");
            writer.AppendFloat(_qualityWallSeconds);
            writer.AppendAscii(",\"qualityAuditObserved\":");
            writer.AppendUInt(HasQualityAuditObserved(vault.Flags) ? 1u : 0u);
            writer.AppendAscii(",\"catastrophicAupDeltaFrame\":");
            writer.AppendUInt(_catastrophicAupDeltaFrame);
            writer.AppendAscii(",\"lastEventHash\":");
            writer.AppendUInt(_lastEventHash);
            writer.AppendByte((byte)'}');
            TryQueueFileWrite(FileTargetResult, dumpScratch, writer.Length);
        }

        private void QueueCsvHeader(NativeArray<byte> csvScratch)
        {
            int length = Shinobu38CsvStreamer.WriteHeader(csvScratch);
            TryQueueFileWrite(FileTargetCsv, csvScratch, length);
        }

        private void SampleFileWriterTelemetry()
        {
            int csvMicros = Volatile.Read(ref _writerLastCsvMicros);
            _lastCsvWriteMs = math.max(0f, csvMicros * 0.001f);

            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterStateHandle, FileWriterStateBufferId, 1, out NativeArray<Shinobu38FileWriterStateDTO> state))
                return;

            try
            {
                Shinobu38FileWriterStateDTO writerState = state[0];
                writerState.LastCsvWriteMicros = csvMicros;
                writerState.LastAnyWriteMicros = Volatile.Read(ref _writerLastAnyMicros);
                writerState.CompletedWrites = unchecked((uint)math.max(0, Volatile.Read(ref _writerCompletedWrites)));
                writerState.WriterFlags |= unchecked((uint)Volatile.Read(ref _writerFaultFlags));
                state[0] = writerState;
            }
            catch (Exception)
            {
                // The main thread can sample during domain teardown; stale writer telemetry is acceptable.
            }
        }

        private bool TryQueueFileWrite(uint target, NativeArray<byte> source, int length)
        {
            if (!source.IsCreated || length <= 0)
                return false;

            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriteCommandsHandle, FileWriteCommandsBufferId, FileWriteQueueCapacity, out NativeArray<Shinobu38FileWriteCommand> commands) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWritePayloadHandle, FileWritePayloadBufferId, FileWritePayloadTotalBytes, out NativeArray<byte> payload) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterStateHandle, FileWriterStateBufferId, 1, out NativeArray<Shinobu38FileWriterStateDTO> writerState) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterCursorHandle, FileWriterCursorBufferId, 1, out NativeArray<Shinobu38FileWriterCursorDTO> cursorBuffer))
            {
                return false;
            }

            ref Shinobu38FileWriterCursorDTO cursor = ref ElementRef(cursorBuffer, 0);
            int safeLength = math.min(length, math.min(source.Length, FileWritePayloadBytes));
            int head = Volatile.Read(ref cursor.Head);
            int next = (head + 1) & FileWriteQueueMask;
            if (next == Volatile.Read(ref cursor.Tail))
            {
                Shinobu38FileWriterStateDTO state = writerState[0];
                state.DroppedWrites++;
                state.ProducerFlags |= FileWriterFlagOverflow;
                writerState[0] = state;
                return false;
            }

            int payloadOffset = head * FileWritePayloadBytes;
            CopyNativeBytes(source, payload, payloadOffset, safeLength);
            commands[head] = new Shinobu38FileWriteCommand
            {
                Sequence = ++_fileWriteSequence,
                PayloadOffset = payloadOffset,
                PayloadLength = safeLength,
                Target = target,
                Flags = 0u
            };
            Volatile.Write(ref cursor.Head, next);
            _fileWriterEvent?.Set();
            return true;
        }

        private void StartFileWriter()
        {
            if (_fileWriterThread != null)
                return;

            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterCursorHandle, FileWriterCursorBufferId, 1, out NativeArray<Shinobu38FileWriterCursorDTO> cursorBuffer))
                return;

            ref Shinobu38FileWriterCursorDTO cursor = ref ElementRef(cursorBuffer, 0);
            Volatile.Write(ref cursor.Running, 1);
            Volatile.Write(ref cursor.Head, 0);
            Volatile.Write(ref cursor.Tail, 0);
            _fileWriteSequence = 0L;
            Volatile.Write(ref _writerLastCsvMicros, 0);
            Volatile.Write(ref _writerLastAnyMicros, 0);
            Volatile.Write(ref _writerCompletedWrites, 0);
            Volatile.Write(ref _writerFaultFlags, 0);
            _fileWriterEvent = new ManualResetEventSlim(false); // COLD ALLOC: SPSC writer wake gate - owner: Shinobu38QaWatchdogRuntime
            _fileWriterThread = new Thread(FileWriterLoop) // COLD ALLOC: background QA file writer - owner: Shinobu38QaWatchdogRuntime
            {
                IsBackground = true,
                Name = "SHINOBU_79_QA_FILE_WRITER"
            };
            _fileWriterThread.Start();
        }

        private void StopFileWriter(bool flushPending)
        {
            Thread writer = _fileWriterThread;
            if (writer == null)
                return;

            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _fileWriterCursorHandle, FileWriterCursorBufferId, 1, out NativeArray<Shinobu38FileWriterCursorDTO> cursorBuffer))
                return;

            ref Shinobu38FileWriterCursorDTO cursor = ref ElementRef(cursorBuffer, 0);
            if (flushPending)
            {
                long start = Stopwatch.GetTimestamp();
                while (Volatile.Read(ref cursor.Tail) != Volatile.Read(ref cursor.Head))
                {
                    _fileWriterEvent?.Set();
                    Thread.Sleep(1);
                    long elapsed = Stopwatch.GetTimestamp() - start;
                    if (elapsed > Stopwatch.Frequency * 5L)
                        break;
                }
            }

            Volatile.Write(ref cursor.Running, 0);
            _fileWriterEvent?.Set();
            writer.Join(2000);
            _fileWriterThread = null;
            _fileWriterEvent?.Dispose();
            _fileWriterEvent = null;
        }

        private void FileWriterLoop()
        {
            FileStream csvStream = null;
            try
            {
                IDataVault vault = _dataVault;
                if (vault == null)
                    return;

                if (!TryResolveWatchdogVaultBuffer(vault, in _fileWriterCursorHandle, FileWriterCursorBufferId, 1, out NativeArray<Shinobu38FileWriterCursorDTO> cursorBuffer))
                    return;

                ref Shinobu38FileWriterCursorDTO cursor = ref ElementRef(cursorBuffer, 0);
                while (Volatile.Read(ref cursor.Running) != 0 ||
                       Volatile.Read(ref cursor.Tail) != Volatile.Read(ref cursor.Head))
                {
                    if (!ReferenceEquals(vault, _dataVault))
                        break;

                    TryReadWaypointCsvOverrideOnWorker();
                    int tail = Volatile.Read(ref cursor.Tail);
                    if (tail == Volatile.Read(ref cursor.Head))
                    {
                        _fileWriterEvent?.Wait(16);
                        _fileWriterEvent?.Reset();
                        continue;
                    }

                    if (!TryReadWatchdogVaultBuffer(vault, in _fileWriteCommandsHandle, FileWriteCommandsBufferId, FileWriteQueueCapacity, out NativeArray<Shinobu38FileWriteCommand> commands) ||
                        !TryReadWatchdogVaultBuffer(vault, in _fileWritePayloadHandle, FileWritePayloadBufferId, FileWritePayloadTotalBytes, out NativeArray<byte> payload))
                    {
                        break;
                    }

                    Shinobu38FileWriteCommand command = commands[tail];
                    long start = Stopwatch.GetTimestamp();
                    if (command.Target == FileTargetCsv)
                    {
                        if (csvStream == null)
                            csvStream = new FileStream(_csvPath, FileMode.Append, FileAccess.Write, FileShare.Read, 1, FileOptions.WriteThrough); // COLD ALLOC: background CSV stream - owner: Shinobu38QaWatchdogRuntime
                        WriteNativeBytes(csvStream, payload, command.PayloadOffset, command.PayloadLength);
                    }
                    else if (command.Target == FileTargetDump)
                    {
                        using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1, FileOptions.WriteThrough)) // COLD ALLOC: background fault dump stream - owner: Shinobu38QaWatchdogRuntime
                            WriteNativeBytes(stream, payload, command.PayloadOffset, command.PayloadLength);
                        using (FileStream stream = new FileStream(_dumpH8Path, FileMode.Create, FileAccess.Write, FileShare.Read, 1, FileOptions.WriteThrough)) // COLD ALLOC: background h8dump stream - owner: Shinobu38QaWatchdogRuntime
                            WriteNativeBytes(stream, payload, command.PayloadOffset, command.PayloadLength);
                    }
                    else if (command.Target == FileTargetResult)
                    {
                        using (FileStream stream = new FileStream(_resultPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1, FileOptions.WriteThrough)) // COLD ALLOC: background result stream - owner: Shinobu38QaWatchdogRuntime
                            WriteNativeBytes(stream, payload, command.PayloadOffset, command.PayloadLength);
                    }

                    long elapsed = Stopwatch.GetTimestamp() - start;
                    int micros = (int)System.Math.Min(int.MaxValue, elapsed * (1000000.0 / Stopwatch.Frequency));
                    Volatile.Write(ref _writerLastAnyMicros, micros);
                    if (command.Target == FileTargetCsv)
                        Volatile.Write(ref _writerLastCsvMicros, micros);
                    Interlocked.Increment(ref _writerCompletedWrites);
                    Volatile.Write(ref cursor.Tail, (tail + 1) & FileWriteQueueMask);
                }
            }
            catch (Exception)
            {
                Volatile.Write(ref _writerFaultFlags, Volatile.Read(ref _writerFaultFlags) | unchecked((int)FileWriterFlagException));
            }
            finally
            {
                csvStream?.Flush();
                csvStream?.Dispose();
            }
        }

        private void ApplyWaypointCsvOverrideIfReady()
        {
            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _waypointIngestStateHandle, WaypointIngestStateBufferId, 1, out NativeArray<Shinobu38WaypointIngestStateDTO> ingestState) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _waypointScratchHandle, WaypointScratchBufferId, CsvOverrideBytes, out NativeArray<byte> waypointScratch) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _waypointsHandle, WaypointsBufferId, RouteCapacity, out NativeArray<Shinobu38RouteWaypointDTO> waypoints) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _mockVaultHandle, MockVaultBufferId, 1, out NativeArray<Shinobu38MockVaultDTO> mockVault))
            {
                return;
            }

            ref Shinobu38WaypointIngestStateDTO ingest = ref ElementRef(ingestState, 0);
            int publishedVersion = Volatile.Read(ref ingest.PublishedVersion);
            if (publishedVersion == Volatile.Read(ref ingest.AppliedVersion))
                return;

            int length = math.clamp(Volatile.Read(ref ingest.PendingLength), 0, waypointScratch.Length);
            int parsed = ParseWaypointCsv(waypointScratch, length, waypoints);
            if (parsed > 0)
            {
                _routeCount = (uint)parsed;
                Shinobu38MockVaultDTO vault = mockVault[0];
                vault.CurrentWaypointIndex = 0;
                mockVault[0] = vault;
                ingest.AppliedCount = parsed;
            }

            Volatile.Write(ref ingest.AppliedVersion, publishedVersion);
        }

        private void TryReadWaypointCsvOverrideOnWorker()
        {
            long now = Stopwatch.GetTimestamp();
            if (now < _nextWaypointPollTicks)
                return;

            _nextWaypointPollTicks = now + Stopwatch.Frequency;
            if (string.IsNullOrEmpty(_waypointCsvPath))
                return;

            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _waypointIngestStateHandle, WaypointIngestStateBufferId, 1, out NativeArray<Shinobu38WaypointIngestStateDTO> ingestState) ||
                !TryResolveWatchdogVaultBuffer(_dataVault, in _waypointScratchHandle, WaypointScratchBufferId, CsvOverrideBytes, out NativeArray<byte> waypointScratch))
            {
                return;
            }

            ref Shinobu38WaypointIngestStateDTO ingest = ref ElementRef(ingestState, 0);
            if (Volatile.Read(ref ingest.PublishedVersion) != Volatile.Read(ref ingest.AppliedVersion))
                return;

            try
            {
                if (!File.Exists(_waypointCsvPath))
                    return;

                long ticks = File.GetLastWriteTimeUtc(_waypointCsvPath).Ticks;
                if (ticks == _lastWaypointCsvTicks)
                    return;

                using (FileStream stream = new FileStream(_waypointCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // COLD ALLOC: FileStream[1] - background waypoint override ingest - owner: Shinobu38QaWatchdogRuntime
                {
                    int length = ReadNativeBytes(stream, waypointScratch);
                    ingest.PendingLength = math.clamp(length, 0, waypointScratch.Length);
                    _lastWaypointCsvTicks = ticks;
                    Volatile.Write(ref ingest.LastSeenTicks, ticks);
                    Volatile.Write(ref ingest.PublishedVersion, unchecked(Volatile.Read(ref ingest.PublishedVersion) + 1));
                }
            }
            catch (IOException)
            {
                ingest.ProducerFlags |= WaypointIngestFlagReadError;
            }
            catch (UnauthorizedAccessException)
            {
                ingest.ProducerFlags |= WaypointIngestFlagReadError;
            }
        }

        private static int ParseWaypointCsv(NativeArray<byte> bytes, int length, NativeArray<Shinobu38RouteWaypointDTO> waypoints)
        {
            int index = 0;
            int count = 0;
            while (index < length && count < RouteCapacity)
            {
                SkipSeparators(bytes, length, ref index);
                if (!TryParseDouble(bytes, length, ref index, out double x))
                    break;
                SkipFieldSeparator(bytes, length, ref index);
                if (!TryParseDouble(bytes, length, ref index, out double y))
                    break;
                SkipFieldSeparator(bytes, length, ref index);
                if (!TryParseDouble(bytes, length, ref index, out double z))
                    break;

                waypoints[count] = new Shinobu38RouteWaypointDTO
                {
                    Aup = new double3(x, y, z),
                    Flags = 1u
                };
                count++;
                SkipToNextLine(bytes, length, ref index);
            }

            return count;
        }

        private void GenerateEmergencyMockRoute(
            NativeArray<WatchdogStateDTO> stateBuffer,
            NativeArray<Shinobu38RouteWaypointDTO> waypoints,
            NativeArray<Shinobu38MockVaultDTO> mockVault)
        {
            double3 cursor = double3.zero;
            waypoints[0] = new Shinobu38RouteWaypointDTO { Aup = new double3(0d, -35d, 5000d), Flags = 1u };
            waypoints[1] = new Shinobu38RouteWaypointDTO { Aup = new double3(-2500d, -45d, 6500d), Flags = 1u };
            waypoints[2] = new Shinobu38RouteWaypointDTO { Aup = new double3(-5000d, -60d, 5000d), Flags = 1u };
            waypoints[3] = new Shinobu38RouteWaypointDTO { Aup = new double3(-5000d, -70d, 0d), Flags = 1u };
            _routeCount = 4u;
            mockVault[0] = new Shinobu38MockVaultDTO
            {
                CurrentAUP = cursor,
                CurrentWaypointIndex = 0,
                Flags = 0u,
                FrameFlags = 0u,
                _pad0 = 0u
            };
            stateBuffer[0] = new WatchdogStateDTO
            {
                CurrentTargetAUP = waypoints[0].Aup,
                DistanceTraveled = 0f,
                ErrorCount = 0u,
                TestDuration = 0f,
                _pad0 = 0u
            };
        }

        private void StartRecorders()
        {
            _gcUsedRecorder = StartMemoryRecorder("GC Used Memory");
            _totalReservedRecorder = StartMemoryRecorder("Total Reserved Memory");
            _gfxUsedRecorder = StartMemoryRecorder("Gfx Used Memory");
        }

        private static ProfilerRecorder StartMemoryRecorder(string statName)
        {
            try
            {
                ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    statName,
                    RecorderCapacity,
                    ProfilerRecorderOptions.Default);
                return recorder.Valid ? recorder : default;
            }
            catch (ArgumentException)
            {
                return default;
            }
        }

        private void DisposeRecorders()
        {
            DisposeRecorder(ref _gcUsedRecorder);
            DisposeRecorder(ref _totalReservedRecorder);
            DisposeRecorder(ref _gfxUsedRecorder);
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();
            recorder = default;
        }

        private void SampleMemoryRecorders()
        {
            _lastGcUsedBytes = ReadRecorderBytes(_gcUsedRecorder, Profiler.GetMonoUsedSizeLong());
            _lastReservedBytes = ReadRecorderBytes(_totalReservedRecorder, Profiler.GetTotalReservedMemoryLong());
            _lastGfxUsedBytes = ReadRecorderBytes(_gfxUsedRecorder, Profiler.GetAllocatedMemoryForGraphicsDriver());
        }

        private static long ReadRecorderBytes(ProfilerRecorder recorder, long fallback)
        {
            if (!recorder.Valid)
                return fallback > 0L ? fallback : 0L;
            return recorder.LastValue > 0L ? recorder.LastValue : 0L;
        }

        private float ResolveSystemHealthIndex01()
        {
            ReadOnlySpan<SystemHealthIndexSignal> snapshot = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            float value = 0f;
            for (int i = 0; i < snapshot.Length; i++)
            {
                float candidate = snapshot[i].Health01;
                if (math.isfinite(candidate))
                    value = math.max(value, math.saturate(candidate));
            }

            return value;
        }

        private float ResolveThermal01()
        {
            float memoryPressure = _lastReservedBytes > 0L
                ? math.saturate((_lastReservedBytes - _baselineReservedBytes) / (256f * BytesPerMegabyte))
                : 0f;
            return math.saturate(memoryPressure + ResolveSystemHealthIndex01() * 0.25f);
        }

        private float ResolveIoPressure01()
        {
            return math.saturate(_lastCsvWriteMs);
        }

        private void SampleQualityWallClock()
        {
            long now = Stopwatch.GetTimestamp();
            long previous = _qualityClockTicks;
            if (previous <= 0L || now <= previous)
            {
                _qualityClockTicks = now;
                return;
            }

            double elapsed = (now - previous) / (double)Stopwatch.Frequency;
            _qualityClockTicks = now;
            if (elapsed > 0d && math.isfinite(elapsed))
                _qualityWallSeconds += (float)System.Math.Min(elapsed, 1.0d);
        }

        private void PublishAutomationInputOverride()
        {
            if (!TryResolveWatchdogVaultBuffer(_dataVault, in _agent36InputHandle, BufferID.ShinobuInputCurrentDto, InputBufferCapacity, out NativeArray<InputStateDTO> inputBuffer))
                return;

            InputStateDTO input = inputBuffer[0];
            PlayerInputState state = default;
            state.MoveDelta = new Vector2(input.MoveAxis.x, input.MoveAxis.y);
            state.LookDelta = new Vector2(input.LookDelta.x, input.LookDelta.y);
            state.ScrollDelta = Vector2.zero;
            state.SteamDeckGyroAimDelta = Vector2.zero;
            state.SteamDeckLeftTrackpad = Vector2.zero;
            state.SteamDeckRightTrackpad = Vector2.zero;
            state.VerticalDelta = 0f;
            state.ActionsBitmask = input.ButtonMask;
            state.PlatformInputFlags = InputMaskAutomation;
            state.CurrentInputSchemeHash = SourceHash;
            CoreDeterminismSignals.TryPublishInputOverride(in state, SystemDispatcher.CurrentFrameId);
        }

        private float ApplyQualityWeightModulation(float testDurationSeconds)
        {
            float weight = ResolveQualityWeightForDuration(testDurationSeconds);
            if (!_qualityOverrideActive || math.abs(weight - _lastForcedQualityWeight) > QualityEpsilon)
            {
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(weight, true);
                _qualityOverrideActive = true;
                _lastForcedQualityWeight = weight;
            }

            return weight;
        }

        private void ReleaseQualityWeightOverride()
        {
            if (!_qualityOverrideActive)
                return;

            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(1f, false);
            _qualityOverrideActive = false;
            _lastForcedQualityWeight = 1f;
        }

        private static float ResolveQualityWeightForDuration(float testDurationSeconds)
        {
            float finiteDuration = math.isfinite(testDurationSeconds) && testDurationSeconds > 0f ? testDurationSeconds : 0f;
            float phase = finiteDuration - math.floor(finiteDuration / QualityCycleSeconds) * QualityCycleSeconds;
            float recoveryGate = Smooth01((phase - QualityClampSeconds) * math.rcp(math.max(QualityReleaseRampSeconds, 0.0001f)));
            float fullGate = Smooth01((phase - QualityClampSeconds - QualityReleaseRampSeconds) * math.rcp(math.max(QualityReleaseRampSeconds, 0.0001f)));
            float t = math.saturate((phase - QualityClampSeconds) / math.max(QualityReleaseRampSeconds, 0.0001f));
            float eased = t * t * (3f - (2f * t));
            float rampWeight = math.lerp(QualityClampWeight, 1f, eased);
            float clampOrRamp = math.lerp(QualityClampWeight, rampWeight, recoveryGate);
            return math.lerp(clampOrRamp, 1f, fullGate);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static int ResolveAupSector(double absoluteMeters)
        {
            if (!math.isfinite(absoluteMeters))
                return 0;

            return ClampToInt(System.Math.Floor(absoluteMeters / 5000d));
        }

        private static int ClampToInt(double value)
        {
            if (!math.isfinite(value))
                return 0;
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private static uint HashAupTelemetry(int sectorX, int sectorY, int sectorZ, int localMmX, int localMmY, int localMmZ)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)sectorX) * 16777619u;
                hash = (hash ^ (uint)sectorY) * 16777619u;
                hash = (hash ^ (uint)sectorZ) * 16777619u;
                hash = (hash ^ (uint)localMmX) * 16777619u;
                hash = (hash ^ (uint)localMmY) * 16777619u;
                hash = (hash ^ (uint)localMmZ) * 16777619u;
                return hash;
            }
        }

        private void ResolveRuntimePaths()
        {
            _csvPath = ResolveProjectPath(CsvRelativePath);
            _resultPath = ResolveProjectPath(ResultRelativePath);
            _dumpPath = ResolveProjectPath(DumpRelativePath);
            _dumpH8Path = ResolveProjectPath(DumpH8RelativePath);
            _waypointCsvPath = ResolveProjectPath(WaypointCsvRelativePath);
        }

        private void ForceRuntimePolicy()
        {
            if (!_runtimePolicyCaptured)
            {
                _previousRunInBackground = Application.runInBackground;
                _previousTargetFrameRate = Application.targetFrameRate;
                _previousVSyncCount = QualitySettings.vSyncCount;
                _runtimePolicyCaptured = true;
            }

            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }

        private void RestoreRuntimePolicy()
        {
            if (!_runtimePolicyCaptured)
                return;

            Application.runInBackground = _previousRunInBackground;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
            _runtimePolicyCaptured = false;
        }

        private static bool ShouldRunStatic()
        {
            if (HasArg("-h8qa") ||
                HasArg("-h8Qa") ||
                HasArg("-h8QaEndurance") ||
                HasArg("-h8QaEndurance10km"))
            {
                return true;
            }

            string value = global::System.Environment.GetEnvironmentVariable(EnvironmentFlagName);
            if (string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return File.Exists(ResolveProjectPathStatic(FlagRelativePath));
        }

        private static bool HasArg(string argName)
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static Shinobu38QaTier ResolveTierFromArgs()
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            const string tierArg = "-h8qaTier";
            int inlineSeparator = tierArg.Length;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Length > inlineSeparator + 1 &&
                    args[i][inlineSeparator] == '=' &&
                    string.Compare(args[i], 0, tierArg, 0, inlineSeparator, StringComparison.OrdinalIgnoreCase) == 0 &&
                    Enum.TryParse(args[i].Substring(inlineSeparator + 1), true, out Shinobu38QaTier inlineTier)) // COLD ALLOC: startup CLI tier parse only.
                {
                    return inlineTier;
                }

                if (i + 1 < args.Length &&
                    string.Equals(args[i], tierArg, StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse(args[i + 1], true, out Shinobu38QaTier tier))
                {
                    return tier;
                }
            }

            return Shinobu38QaTier.Low;
        }

        private static float TryReadFloatArg(string name, float fallback)
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            int inlineSeparator = name.Length;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Length > inlineSeparator + 1 &&
                    args[i][inlineSeparator] == '=' &&
                    string.Compare(args[i], 0, name, 0, inlineSeparator, StringComparison.OrdinalIgnoreCase) == 0 &&
                    float.TryParse(args[i].AsSpan(inlineSeparator + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float inlineValue))
                {
                    return inlineValue;
                }

                if (i + 1 < args.Length &&
                    string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static uint ResolveHardwareFlags()
        {
            uint flags = 0u;
            if (SystemInfo.graphicsMemorySize <= 2048)
                flags |= 1u;
            if (SystemInfo.processorCount <= 4)
                flags |= 1u << 1;
            if (Application.isBatchMode)
                flags |= 1u << 2;
            return flags;
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

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        private static JobHandle ScheduleClear<T>(NativeArray<T> array, JobHandle dependency) where T : unmanaged
        {
            if (!array.IsCreated)
                return dependency;

            Shinobu38MemClearJob<T> job = new Shinobu38MemClearJob<T> { Buffer = array };
            return job.Schedule(dependency);
        }

        private static void SkipSeparators(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t' && c != (byte)'\r' && c != (byte)'\n')
                    return;
                index++;
            }
        }

        private static void SkipFieldSeparator(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)';' || c == (byte)' ' || c == (byte)'\t')
                {
                    index++;
                    continue;
                }

                return;
            }
        }

        private static void SkipToNextLine(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte c = bytes[index++];
                if (c == (byte)'\n')
                    return;
            }
        }

        private static bool TryParseDouble(NativeArray<byte> bytes, int length, ref int index, out double value)
        {
            value = 0d;
            SkipSeparators(bytes, length, ref index);
            bool negative = false;
            if (index < length && bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            bool any = false;
            long integer = 0L;
            while (index < length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                integer = (integer * 10L) + (c - (byte)'0');
                index++;
                any = true;
            }

            double fraction = 0d;
            double scale = 1d;
            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < length)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    fraction = (fraction * 10d) + (c - (byte)'0');
                    scale *= 10d;
                    index++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = integer + (fraction / scale);
            if (negative)
                value = -value;
            return true;
        }

        private static void WriteUInt(NativeArray<byte> buffer, ref int cursor, uint value)
        {
            buffer[cursor++] = (byte)value;
            buffer[cursor++] = (byte)(value >> 8);
            buffer[cursor++] = (byte)(value >> 16);
            buffer[cursor++] = (byte)(value >> 24);
        }

        private static void WriteFloat(NativeArray<byte> buffer, ref int cursor, float value)
        {
            WriteUInt(buffer, ref cursor, math.asuint(value));
        }

        private static unsafe int ReadNativeBytes(FileStream stream, NativeArray<byte> destination)
        {
            if (!destination.IsCreated || stream == null)
                return 0;

            void* ptr = destination.GetUnsafePtr();
            return stream.Read(new Span<byte>(ptr, destination.Length));
        }

        private static unsafe void CopyNativeBytes(NativeArray<byte> source, NativeArray<byte> destination, int destinationOffset, int length)
        {
            if (!source.IsCreated || !destination.IsCreated || length <= 0)
                return;

            int safeLength = math.min(length, math.min(source.Length, destination.Length - destinationOffset));
            if (safeLength <= 0)
                return;

            void* sourcePtr = source.GetUnsafeReadOnlyPtr();
            void* destinationPtr = (byte*)destination.GetUnsafePtr() + destinationOffset;
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, safeLength);
        }

        private static unsafe void WriteNativeBytes(FileStream stream, NativeArray<byte> source, int length)
        {
            WriteNativeBytes(stream, source, 0, length);
        }

        private static unsafe void WriteNativeBytes(FileStream stream, NativeArray<byte> source, int offset, int length)
        {
            if (!source.IsCreated || stream == null || length <= 0)
                return;

            int safeLength = math.min(length, source.Length - offset);
            if (safeLength <= 0)
                return;

            void* ptr = (byte*)source.GetUnsafeReadOnlyPtr() + offset;
            stream.Write(new ReadOnlySpan<byte>(ptr, safeLength));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct Shinobu38MemClearJob<T> : IJob where T : unmanaged
        {
            [NoAlias]
            public NativeArray<T> Buffer;

            public void Execute()
            {
                void* ptr = Buffer.GetUnsafePtr();
                UnsafeUtility.MemClear(ptr, (long)Buffer.Length * UnsafeUtility.SizeOf<T>());
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct BotNavigationJob : IJob
        {
            [NoAlias]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<WatchdogStateDTO> State;
            [NoAlias]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<TelemetrySnapshotDTO> Snapshot;
            [NoAlias]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<InputStateDTO> InputBuffer;
            [NoAlias]
            [ReadOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Shinobu38RouteWaypointDTO> Waypoints;
            [NoAlias]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<MockRebaseSignal> RebaseSignals;
            [NoAlias]
            [ReadOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Shinobu38TuningDTO> Tuning;
            [NoAlias]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Shinobu38MockVaultDTO> MockVault;
            public float DeltaTime;
            public float WallDeltaTime;
            public uint Frame;
            public uint RouteCount;
            public float TargetDistanceMeters;
            public long BaseGcBytes;
            public long CurrentGcBytes;
            public long CurrentVramBytes;
            public float SystemHealthIndex01;
            public float QualityWeight;

            public void Execute()
            {
                ref WatchdogStateDTO state = ref UnsafeUtility.AsRef<WatchdogStateDTO>(State.GetUnsafePtr());
                Shinobu38MockVaultDTO vault = MockVault[0];
                Shinobu38TuningDTO tuning = Tuning[0];
                if (RouteCount == 0u)
                    return;

                int routeCount = (int)math.min(RouteCount, (uint)Waypoints.Length);
                int waypointIndex = math.clamp(vault.CurrentWaypointIndex, 0, routeCount - 1);
                double3 target = Waypoints[waypointIndex].Aup;
                double3 current = vault.CurrentAUP;
                uint flags = vault.Flags & (VaultFlagMemoryLeakDetected |
                                            VaultFlagSurvivalPressureEmergency |
                                            VaultFlagFatal |
                                            VaultFlagCsvSlow |
                                            VaultFlagStressRecoveryObserved |
                                            VaultFlagActualAupSampled |
                                            VaultFlagActualAupJitter);
                uint frameFlags = 0u;
                if (!math.all(math.isfinite(target)) || !math.all(math.isfinite(current)))
                {
                    target = double3.zero;
                    current = double3.zero;
                    flags |= VaultFlagFatal;
                    frameFlags |= TelemetryFlagFatal;
                    state.ErrorCount++;
                }

                double3 toTarget = target - current;
                float3 toTargetFloat = new float3((float)toTarget.x, (float)toTarget.y, (float)toTarget.z);
                float remainingSq = math.lengthsq(toTargetFloat);
                if (remainingSq < 25f && waypointIndex + 1 < routeCount)
                {
                    waypointIndex++;
                    target = Waypoints[waypointIndex].Aup;
                    toTarget = target - current;
                    toTargetFloat = new float3((float)toTarget.x, (float)toTarget.y, (float)toTarget.z);
                    remainingSq = math.lengthsq(toTargetFloat);
                }

                float3 desired = remainingSq > 0.0001f ? toTargetFloat * math.rsqrt(remainingSq) : new float3(0f, 0f, 1f);
                double3 localToTarget = current - target;
                float3 localCurrent = new float3((float)localToTarget.x, (float)localToTarget.y, (float)localToTarget.z);
                float3 ahead = localCurrent + desired * 12f;
                float sdf = Shinobu38MockTerrainSdf.SampleDistance(ahead);
                if (sdf < 10f)
                {
                    float quality = math.saturate(QualityWeight);
                    float richNormalGate = Smooth01((quality - RichNormalFadeStart01) * math.rcp(math.max(0.0001f, 1f - RichNormalFadeStart01)));
                    float3 cheapNormal = math.normalizesafe(new float3(-desired.x * 0.25f, 1f, -desired.z * 0.25f), new float3(0f, 1f, 0f));
                    float3 richNormal = Shinobu38MockTerrainSdf.SampleNormal(ahead);
                    float normalBlend = richNormalGate * quality * quality * (3f - (2f * quality));
                    float3 normal = math.normalizesafe(math.lerp(cheapNormal, richNormal, normalBlend), new float3(0f, 1f, 0f));
                    float avoid01 = math.saturate((10f - sdf) * 0.1f);
                    desired = math.normalizesafe(desired + normal * (tuning.ObstacleAvoidanceStrength * avoid01), new float3(0f, 0f, 1f));
                    frameFlags |= TelemetryFlagAvoidance;
                }

                float speed = math.max(0.1f, tuning.SwimSpeed);
                float traveled = speed * math.max(0f, DeltaTime);
                current += (double3)(desired * traveled);
                state.DistanceTraveled += traveled;
                state.TestDuration += math.max(0f, DeltaTime);
                state.CurrentTargetAUP = target;
                if (!math.all(math.isfinite(current)))
                {
                    current = double3.zero;
                    flags |= VaultFlagFatal;
                    frameFlags |= TelemetryFlagFatal;
                    state.ErrorCount++;
                }

                vault.CurrentAUP = current;
                vault.CurrentWaypointIndex = waypointIndex;

                MockRebaseSignal rebase = default;
                if ((Frame & 2047u) == 1024u)
                {
                    rebase.OffsetAUP = new double3(100d, 0d, -100d);
                    rebase.Frame = Frame;
                    rebase.Flags = 1u;
                    vault.CurrentAUP += rebase.OffsetAUP;
                    state.CurrentTargetAUP += rebase.OffsetAUP;
                    frameFlags |= TelemetryFlagRebase;
                    if (!math.all(math.isfinite(vault.CurrentAUP)) || !math.all(math.isfinite(state.CurrentTargetAUP)))
                    {
                        vault.CurrentAUP = double3.zero;
                        state.CurrentTargetAUP = double3.zero;
                        flags |= VaultFlagFatal;
                        frameFlags |= TelemetryFlagFatal;
                        state.ErrorCount++;
                    }
                }

                float2 planar = math.normalizesafe(new float2(desired.x, desired.z), new float2(0f, 1f));
                uint inputMask = InputMaskSprint;
                float firePhase = state.TestDuration - (math.floor(state.TestDuration / 30f) * 30f);
                if (firePhase < 0.25f)
                    inputMask |= InputMaskPrimaryFire;

                InputBuffer[0] = new InputStateDTO
                {
                    LookDelta = planar * 0.018f,
                    MoveAxis = planar,
                    ButtonMask = inputMask | InputMaskAutomation
                };

                double3 localAfterMove = vault.CurrentAUP - state.CurrentTargetAUP;
                double3 floatDowncast = state.CurrentTargetAUP + (double3)new float3((float)localAfterMove.x, (float)localAfterMove.y, (float)localAfterMove.z);
                double3 jitter = vault.CurrentAUP - floatDowncast;
                double jitterMeters = math.all(math.isfinite(jitter)) ? math.sqrt(math.lengthsq(jitter)) : 1d;
                float jitterMm = (float)(jitterMeters * 1000d);
                if (jitterMeters > 0.001d)
                    state.ErrorCount++;

                float frameTimeMs = math.max(0f, WallDeltaTime) * 1000f;
                float gcDelta = math.max(0f, CurrentGcBytes - BaseGcBytes);
                Snapshot[0] = new TelemetrySnapshotDTO
                {
                    FrameTimeMs = frameTimeMs,
                    GcAllocBytes = gcDelta,
                    VramUsed = (float)(CurrentVramBytes / (double)BytesPerMegabyte),
                    AupJitterError = jitterMm
                };

                if (SystemHealthIndex01 >= 0.9f)
                    flags |= VaultFlagSurvivalPressureEmergency;
                vault.Flags = flags;
                vault.FrameFlags = frameFlags;
                MockVault[0] = vault;
                RebaseSignals[0] = rebase;
            }

        }
    }

    public static class Shinobu38MockTerrainSdf
    {
        public static float SampleDistance(float3 point)
        {
            float waveA = MathLodApproximation.ApproxSinBhaskara(point.z * 0.013f) * 9f;
            float waveB = MathLodApproximation.ApproxSinBhaskara(point.x * 0.017f + point.z * 0.007f) * 6f;
            float caveRadius = 26f + waveA + waveB;
            float vertical = math.abs(point.y + 50f + MathLodApproximation.ApproxSinBhaskara(point.z * 0.009f) * 12f);
            float lateral = math.abs(MathLodApproximation.ApproxSinBhaskara(point.x * 0.006f) * 18f);
            return caveRadius - math.max(vertical, lateral);
        }

        public static float3 SampleNormal(float3 point)
        {
            const float eps = 0.75f;
            float dx = SampleDistance(point + new float3(eps, 0f, 0f)) - SampleDistance(point - new float3(eps, 0f, 0f));
            float dy = SampleDistance(point + new float3(0f, eps, 0f)) - SampleDistance(point - new float3(0f, eps, 0f));
            float dz = SampleDistance(point + new float3(0f, 0f, eps)) - SampleDistance(point - new float3(0f, 0f, eps));
            return math.normalizesafe(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }
    }

    internal static class Shinobu38CsvStreamer
    {
        public static int WriteHeader(NativeArray<byte> scratch)
        {
            Shinobu38AsciiBuffer writer = new Shinobu38AsciiBuffer(scratch);
            writer.AppendAscii("Timestamp,WallSeconds,AUP_X,AUP_Y,AUP_Z,FrameTimeMs,GCUsedBytes,ReservedBytes,VRAMBytes,SHI,QualityWeight,Thermal,IO,AUPJitterMm,DistanceMeters,RebaseCount,HardwareFlags,VaultFlags\n");
            return writer.Length;
        }

        public static int WriteRecord(
            NativeArray<byte> scratch,
            uint frame,
            float timestamp,
            float wallSeconds,
            in double3 aup,
            float frameTimeMs,
            long gcUsedBytes,
            long reservedBytes,
            long vramBytes,
            float shi,
            float qualityWeight,
            float thermal,
            float io,
            float jitterMm,
            float distanceMeters,
            uint rebaseCount,
            uint hardwareFlags,
            uint vaultFlags)
        {
            Shinobu38AsciiBuffer writer = new Shinobu38AsciiBuffer(scratch);
            writer.AppendFloat(timestamp);
            writer.AppendComma();
            writer.AppendFloat(wallSeconds);
            writer.AppendComma();
            writer.AppendDouble(aup.x);
            writer.AppendComma();
            writer.AppendDouble(aup.y);
            writer.AppendComma();
            writer.AppendDouble(aup.z);
            writer.AppendComma();
            writer.AppendFloat(frameTimeMs);
            writer.AppendComma();
            writer.AppendLong(gcUsedBytes);
            writer.AppendComma();
            writer.AppendLong(reservedBytes);
            writer.AppendComma();
            writer.AppendLong(vramBytes);
            writer.AppendComma();
            writer.AppendFloat(shi);
            writer.AppendComma();
            writer.AppendFloat(qualityWeight);
            writer.AppendComma();
            writer.AppendFloat(thermal);
            writer.AppendComma();
            writer.AppendFloat(io);
            writer.AppendComma();
            writer.AppendFloat(jitterMm);
            writer.AppendComma();
            writer.AppendFloat(distanceMeters);
            writer.AppendComma();
            writer.AppendUInt(rebaseCount);
            writer.AppendComma();
            writer.AppendUInt(hardwareFlags);
            writer.AppendComma();
            writer.AppendUInt(vaultFlags);
            writer.AppendByte((byte)'\n');
            return writer.Length;
        }
    }

    internal unsafe struct Shinobu38AsciiBuffer
    {
        private readonly byte* _buffer;
        private readonly int _capacity;
        private int _cursor;

        public Shinobu38AsciiBuffer(NativeArray<byte> buffer)
        {
            _buffer = buffer.IsCreated ? (byte*)buffer.GetUnsafePtr() : null;
            _capacity = buffer.IsCreated ? buffer.Length : 0;
            _cursor = 0;
        }

        public int Length => _cursor;

        public void AppendComma()
        {
            AppendByte((byte)',');
        }

        public void AppendAscii(string value)
        {
            for (int i = 0; i < value.Length; i++)
                AppendByte(value[i] <= 127 ? (byte)value[i] : (byte)'?');
        }

        public void AppendByte(byte value)
        {
            if (_buffer != null && _cursor < _capacity)
                _buffer[_cursor++] = value;
        }

        public void AppendUInt(uint value)
        {
            if (value == 0u)
            {
                AppendByte((byte)'0');
                return;
            }

            int start = _cursor;
            while (value > 0u)
            {
                AppendByte((byte)('0' + (value % 10u)));
                value /= 10u;
            }
            Reverse(start, _cursor - 1);
        }

        public void AppendLong(long value)
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
                AppendByte((byte)('0' + (value % 10L)));
                value /= 10L;
            }
            Reverse(start, _cursor - 1);
        }

        public void AppendFloat(float value)
        {
            if (!math.isfinite(value))
            {
                AppendByte((byte)'0');
                return;
            }

            AppendDouble(value);
        }

        public void AppendDouble(double value)
        {
            if (!math.isfinite(value))
            {
                AppendByte((byte)'0');
                return;
            }

            if (value < 0d)
            {
                AppendByte((byte)'-');
                value = -value;
            }

            long milli = (long)System.Math.Round(value * 1000d);
            AppendLong(milli / 1000L);
            AppendByte((byte)'.');
            int frac = (int)(milli % 1000L);
            AppendByte((byte)('0' + ((frac / 100) % 10)));
            AppendByte((byte)('0' + ((frac / 10) % 10)));
            AppendByte((byte)('0' + (frac % 10)));
        }

        private void Reverse(int first, int last)
        {
            while (first < last)
            {
                byte tmp = _buffer[first];
                _buffer[first] = _buffer[last];
                _buffer[last] = tmp;
                first++;
                last--;
            }
        }
    }
}
#endif
