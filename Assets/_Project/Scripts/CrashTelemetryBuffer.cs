using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Core
{
    /// <summary>
    /// Captures a bounded stream of low-cost runtime telemetry and exports a binary snapshot on fault conditions.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9500)] // Runs after GameTickManager singleton bootstrap and before most gameplay systems.
    public sealed class CrashTelemetryBuffer : MonoBehaviour, ITickable, IUpdatable, IFixedTickable, IGlobalRegistryHotSwapListener
    {
        private const int RingCapacity = 1024;
        private const int RingCapacityMask = RingCapacity - 1;
        private const int ExportSnapshotEntries = 1000;
        private const int ExportCooldownFrames = 30;
        private const int TelemetryEntrySizeBytes = 64;
        private const int CrashExportHeaderSizeBytes = 16;
        private const int ExportScratchSizeBytes = CrashExportHeaderSizeBytes + (ExportSnapshotEntries * TelemetryEntrySizeBytes);
        private const int ExportStateIdle = 0;
        private const int ExportStateQueued = 1;
        private const int LiveTelemetryStateIdle = 0;
        private const int LiveTelemetryStateQueued = 1;
        private const int RecorderCapacity = 1;
        private const int LiveTelemetryWriteIntervalFrames = 60;
        private const int LiveTelemetryRecordSizeBytes = 32;
        private const float PlayerResolveCooldownSeconds = 1f;
        private const float OriginShiftTelemetryIntervalSeconds = 1f;
        private const float SevereFrameTimeSeconds = 0.025f;
        private const float CriticalFrameTimeSeconds = 0.033f;
        private const float MaximumTrackedWorldMagnitude = 1000000f;
        private const float MaximumReservedMemoryMb = 4096f;
        private const float BytesToMegabytes = GlobalTelemetryBus.BytesToMegabytes;
        private const float NanosecondsToMilliseconds = 0.000001f;
        private const float SignedTemperatureToUnit = 0.006666667f;
        private const uint LiveTelemetryMagic = 0x4D4C4554u; // "TELM"
        private const uint LiveTelemetryVersion = 1u;
        private const ulong BinaryMagic = 0x00384E4F54434548ul; // "HECTON8\0" in little-endian byte order.
        private const string ExportFilePrefix = "crash_";
        private const string ExportFileExtension = ".h8dump";
        private const string ExportTimestampFormat = "yyyyMMdd_HHmmss_fff";
        private const string LiveTelemetryFileName = "runtime_telemetry.bin";
        private const string CrashTelemetryFileName = "BLACKBOX_CRASH.h8dump";
        private const string BlackBoxExportThreadName = "H8.BlackBoxExport";
        private const int BlackBoxExportThreadJoinMilliseconds = 250;
        private const int ProfilerRecorderHandleScratchCapacity = 256;
        private const int BlackBoxExportFailureCounterMax = 1024;
        private const int BlackBoxExportDroppedCounterMax = 1024;
        private const int BlackBoxExportSuppressedCounterMax = 1024;
        private const long PersistentMemoryBudgetBytes = 262144L;
        private const string MemoryBudgetOwnerName = "CrashTelemetryBuffer";
        private static readonly string[] _FrameTimeCandidates =
        {
            "CPU Total Frame Time",
            "Frame Time"
        };

        private static readonly string[] _GcAllocCandidates = { "GC Allocated In Frame" };

        private static readonly WaitCallback _backgroundLiveTelemetryCallback = ExecuteBackgroundLiveTelemetryWrite;
        private static readonly uint _audioOverflowDropWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("CrashTelemetry.AudioOverflowDrop"));
        private static readonly uint _audioOverflowBufferContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("CrashTelemetry.NativeAudioFrameRingBuffer"));
        private static readonly uint _bootPerfWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("BOOT_PERF_WARNING"));
        private const double BootstrapPerfWarningThresholdMilliseconds = 200d;
        private static int _runtimeFaultFlags;
        private static int _pendingAudioOverflowDropCount;
        private static int _pendingAudioOverflowBufferedFrames;
        private static int _pendingAudioOverflowWritableFrames;
        private static int _pendingAudioDspStats;
        private static int _pendingActiveDspVoices;
        private static int _pendingSdfSampleTimeMicroseconds;
        private static int _pendingAudioBufferUnderruns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterBootstrapTelemetryReporter()
        {
            BootstrapStatus.RegisterSafeHaltTelemetryReporter(ReportBootstrapSafeHalt);
        }

        [Flags]
        private enum ErrorBits : uint
        {
            None = 0u,
            MissingPlayer = 1u << 0,
            NonFiniteDeltaTime = 1u << 1,
            NonFinitePlayerPosition = 1u << 2,
            FrameBudgetExceeded = 1u << 3,
            ReservedMemoryExceeded = 1u << 4,
            ExceptionLogged = 1u << 5,
            ErrorLogged = 1u << 6,
            OutOfBoundsPlayerPosition = 1u << 7,
            NanPhysics = 1u << 8,
            EventCascadeWarning = 1u << 9,
            TemporalCompression = 1u << 10,
            BusCongestionWarning = 1u << 11,
            KineticAnomaly = 1u << 12,
            LateFrameLoadShedding = 1u << 13,
            BootstrapSafeHalt = 1u << 14,
            RuntimeWatchdogStall = 1u << 15,
            AupJitterCorrection = 1u << 16,
            CriticalRecovery = 1u << 17,
            CriticalMemoryPressure = 1u << 18,
            AudioOverflowDropWarning = 1u << 19,
            BootPerfWarning = 1u << 20,
            CriticalPerformanceSpike = 1u << 21,
            LatencyCrime = 1u << 22,
            NativeFragmentationRisk = 1u << 23,
            StaleBufferCrime = 1u << 24,
            BlackBoxExportFault = 1u << 25,
            NativeTransientLeak = 1u << 26,
            BlackBoxExportDropped = 1u << 27,
            BlackBoxExportSuppressed = 1u << 28,
            RuntimeMemorySpike = 1u << 29,
            PhysiologyNan = 1u << 30,
            JobAdmissionStarvation = 1u << 31,
        }

        [Flags]
        private enum SystemBits : uint
        {
            None = 0u,
            Physics = 1u << 0,
            Voxel = 1u << 1,
            AI = 1u << 2,
            Fluid = 1u << 3,
            Bootstrap = 1u << 4,
            OriginShift = 1u << 5,
            EventBus = 1u << 6,
            Save = 1u << 7,
            Audio = 1u << 8,
            Input = 1u << 9,
            Memory = 1u << 10,
            Physiology = 1u << 11,
            TimeDilation = 1u << 12,
            Scheduler = 1u << 13,
            Vfx = 1u << 14,
            WorldStreaming = 1u << 15,
            Rendering = 1u << 16,
        }

        private enum ExportReason : uint
        {
            None = 0u,
            ErrorFlags = 1u,
            UnityException = 2u,
            UnityError = 3u,
            ApplicationQuit = 4u,
            AppDomainUnhandledException = 5u,
            BootstrapPhaseDuration = 6u,
            BusCongestionWarning = 7u,
            KineticAnomaly = 8u,
            LateFrameLoadShedding = 9u,
            BootstrapSafeHalt = 10u,
            RuntimeWatchdogStall = 11u,
            AupJitterCorrection = 12u,
            CriticalRecovery = 13u,
            CriticalMemoryPressure = 14u,
            AudioOverflowDropWarning = 15u,
            BootPerfWarning = 16u,
            CriticalPerformanceSpike = 17u,
            LatencyCrime = 18u,
            NativeFragmentationRisk = 19u,
            StaleBufferCrime = 20u,
            BlackBoxExportFault = 21u,
            NativeTransientLeak = 22u,
            BlackBoxExportDropped = 23u,
            BlackBoxExportSuppressed = 24u,
            RuntimeMemorySpike = 25u,
            PeakStressEvent = 26u,
            PhysiologyNan = 27u,
            JobAdmissionStarvation = 28u,
        }

        private const uint ExportInternalFaultMask =
            (uint)ErrorBits.BlackBoxExportFault |
            (uint)ErrorBits.BlackBoxExportDropped |
            (uint)ErrorBits.BlackBoxExportSuppressed;

        private const uint ExportCooldownBypassMask =
            (uint)ErrorBits.NanPhysics |
            (uint)ErrorBits.CriticalPerformanceSpike |
            (uint)ErrorBits.CriticalMemoryPressure |
            (uint)ErrorBits.RuntimeMemorySpike |
            (uint)ErrorBits.PhysiologyNan |
            (uint)ErrorBits.JobAdmissionStarvation |
            (uint)ErrorBits.RuntimeWatchdogStall |
            (uint)ErrorBits.BootstrapSafeHalt;

        [StructLayout(LayoutKind.Explicit, Size = CrashExportHeaderSizeBytes)]
        private struct CrashExportHeader
        {
            [FieldOffset(0)]
            public ulong Magic;
            [FieldOffset(8)]
            public uint EntryCount;
            [FieldOffset(12)]
            public uint StructSizeBytes;
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        private struct TelemetryEntry
        {
            [FieldOffset(0)]
            public uint FrameIndex;
            [FieldOffset(4)]
            public uint SystemMask;
            [FieldOffset(8)]
            public float DeltaTime;
            [FieldOffset(12)]
            public float LatencyMs;
            [FieldOffset(16)]
            public float GpuFrameTime;
            [FieldOffset(20)]
            public float MemoryUsedMb;
            [FieldOffset(24)]
            public float3 PlayerAup;
            [FieldOffset(36)]
            public uint ActiveChunkCount;
            [FieldOffset(40)]
            public uint ErrorFlags;
            [FieldOffset(44)]
            public uint ExportReason;
            [FieldOffset(48)]
            public uint AupShiftSequence;
            [FieldOffset(52)]
            public uint AiStatePacked;
            [FieldOffset(52)]
            public uint VelocityPacked;
            [FieldOffset(56)]
            public uint SubsystemHeatPacked;
            [FieldOffset(56)]
            public uint GcAllocBytes;
            [FieldOffset(60)]
            public uint LastOriginShiftFrame;
        }

        [StructLayout(LayoutKind.Explicit, Size = LiveTelemetryRecordSizeBytes)]
        private struct LiveTelemetryRecord
        {
            [FieldOffset(0)]
            public uint Magic;
            [FieldOffset(4)]
            public uint Version;
            [FieldOffset(8)]
            public uint FrameIndex;
            [FieldOffset(12)]
            public uint ActiveChunkCount;
            [FieldOffset(16)]
            public uint GcAllocBytes;
            [FieldOffset(20)]
            public float CpuFrameTimeMs;
            [FieldOffset(24)]
            public float DeltaTime;
            [FieldOffset(28)]
            public float ReservedMemoryMb;
        }

        private NativeArray<TelemetryEntry> _ringBuffer;
        private NativeArray<TelemetryEntry> _exportSnapshot;
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
        private HectonPlayerMovement _playerMovement;
        private HectonSurvivalSystem _survivalSystem;
        private float _playerResolveCooldown;
        private float _nextOriginShiftTelemetryTime;
        private float _lastLatencyMs;
        private long _writeCursor;
        private int _stickyErrorFlags;
        private bool _runtimeRegistered;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _registeredHotSwap;
        private bool _subscribed;
        private int _fluidRuntimePresent;
        private int _saveRuntimePresent;
        private int _thermodynamicsRuntimePresent;
        private int _lastExportFrame = int.MinValue;
        private int _threadedFaultFlags;
        private int _exportState;
        private int _liveTelemetryWriteState;
        private int _pendingExportSnapshotCount;
        private int _pendingExportBytes;
        private int _pendingExportFrame = int.MinValue;
        private int _pendingBlackBoxExportFailureCount;
        private int _pendingBlackBoxExportDroppedCount;
        private int _pendingBlackBoxExportSuppressedCount;
        private string _liveTelemetryPath;
        private string _crashTelemetryPath;
        private FileStream _liveTelemetryStream;
        private FileStream _crashTelemetryStream;
        private NativeArray<byte> _exportScratch;
        private LiveTelemetryRecord _pendingLiveTelemetryRecord;
        // COLD ALLOC: object[1] - live telemetry file write/dispose gate - owner: CrashTelemetryBuffer
        private readonly object _liveTelemetryFileGate = new object();
        // COLD ALLOC: object[1] - crash export file write/dispose gate - owner: CrashTelemetryBuffer
        private readonly object _crashTelemetryFileGate = new object();
        // COLD ALLOC: byte[32] - portable live telemetry file scratch - owner: CrashTelemetryBuffer
        private readonly byte[] _liveTelemetryFileScratch = new byte[LiveTelemetryRecordSizeBytes];
        // COLD ALLOC: byte[64016] - portable crash export file scratch - owner: CrashTelemetryBuffer
        private readonly byte[] _crashExportFileScratch = new byte[ExportScratchSizeBytes];
        // COLD ALLOC: object[1] - BLACKBOX export thread lifecycle gate - owner: CrashTelemetryBuffer
        private readonly object _blackBoxExportThreadGate = new object();
        private Thread _blackBoxExportThread;
        private AutoResetEvent _blackBoxExportSignal;
        private int _blackBoxExportStopRequested;
        private ProfilerRecorder _frameTimeRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private int _lastLiveTelemetryWriteFrame = int.MinValue;
        // COLD ALLOC: FrameTiming[1] - reusable GPU timing sample buffer - owner: CrashTelemetryBuffer
        private readonly FrameTiming[] _frameTimingScratch = new FrameTiming[1];
        // COLD ALLOC: List<ProfilerRecorderHandle>[256] - profiler recorder resolution scratch - owner: CrashTelemetryBuffer
        private readonly List<ProfilerRecorderHandle> _availableProfilerHandles = new List<ProfilerRecorderHandle>(ProfilerRecorderHandleScratchCapacity);

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only view model for the latest retained crash telemetry frames.
        /// </summary>
        public readonly struct EditorSnapshotEntry
        {
            public readonly uint FrameIndex;
            public readonly uint SystemMask;
            public readonly float DeltaTime;
            public readonly float LatencyMs;
            public readonly float GpuFrameTime;
            public readonly float MemoryUsedMb;
            public readonly Vector3 PlayerAup;
            public readonly uint ActiveChunkCount;
            public readonly uint VelocityPacked;
            public readonly uint GcAllocBytes;
            public readonly uint ErrorFlags;
            public readonly uint ExportReason;

            public EditorSnapshotEntry(
                uint frameIndex,
                uint systemMask,
                float deltaTime,
                float latencyMs,
                float gpuFrameTime,
                float memoryUsedMb,
                Vector3 playerAup,
                uint activeChunkCount,
                uint velocityPacked,
                uint gcAllocBytes,
                uint errorFlags,
                uint exportReason)
            {
                FrameIndex = frameIndex;
                SystemMask = systemMask;
                DeltaTime = deltaTime;
                LatencyMs = latencyMs;
                GpuFrameTime = gpuFrameTime;
                MemoryUsedMb = memoryUsedMb;
                PlayerAup = playerAup;
                ActiveChunkCount = activeChunkCount;
                VelocityPacked = velocityPacked;
                GcAllocBytes = gcAllocBytes;
                ErrorFlags = errorFlags;
                ExportReason = exportReason;
            }
        }
#endif

        /// <summary>
        /// Returns true when the telemetry ring buffer is initialized.
        /// </summary>
        public bool IsInitialized => _ringBuffer.IsCreated;

        /// <summary>
        /// Ensures a live telemetry owner exists.
        /// </summary>
        /// <returns>Live telemetry owner.</returns>
        public static CrashTelemetryBuffer EnsureRuntimeInstance()
        {
            CrashTelemetryBuffer registeredInstance = GlobalRegistry.CrashTelemetry;
            if (registeredInstance != null)
                return registeredInstance;

            GameObject telemetryObject = new GameObject("[CrashTelemetryBuffer]");
            return telemetryObject.AddComponent<CrashTelemetryBuffer>();
        }

        /// <summary>
        /// Reports a physics NaN recovery into the telemetry error stream.
        /// </summary>
        public static void ReportNanPhysicsRecovery()
        {
            OrRuntimeFaultFlags((int)ErrorBits.NanPhysics);

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteNanPhysicsRecoveryTelemetry(Vector3.zero, Vector3.zero);
        }

        /// <summary>
        /// Reports a physics NaN recovery and returns the finite coordinate that should replace the invalid AUP/runtime position.
        /// </summary>
        /// <param name="invalidRuntimePosition">Rejected runtime-space position.</param>
        /// <param name="lastKnownGoodRuntimePosition">Last finite runtime-space position.</param>
        /// <returns>Finite replacement coordinate.</returns>
        public static Vector3 ReportNanPhysicsRecovery(Vector3 invalidRuntimePosition, Vector3 lastKnownGoodRuntimePosition)
        {
            Vector3 recoveredRuntimePosition = IsFiniteVector(lastKnownGoodRuntimePosition)
                ? lastKnownGoodRuntimePosition
                : Vector3.zero;

            OrRuntimeFaultFlags((int)ErrorBits.NanPhysics);

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return recoveredRuntimePosition;

            instance.WriteNanPhysicsRecoveryTelemetry(invalidRuntimePosition, recoveredRuntimePosition);
            return recoveredRuntimePosition;
        }

        /// <summary>
        /// Reports a critical save-system fault into the crash telemetry stream.
        /// </summary>
        public static void ReportSaveSystemCriticalFault()
        {
            OrRuntimeFaultFlags((int)ErrorBits.ErrorLogged);
        }

        /// <summary>
        /// Reports a CRITICAL_RECOVERY save backup promotion into crash telemetry.
        /// </summary>
        public static void ReportCriticalRecovery()
        {
            int flags = unchecked((int)ErrorBits.CriticalRecovery);
            OrRuntimeFaultFlags(flags);

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null)
                return;

            instance.OrThreadedFaultFlags(flags);
        }

        /// <summary>
        /// Records an out-of-memory precursor event and queues a crash-telemetry snapshot.
        /// </summary>
        public static void ReportCriticalMemoryPressure(long reservedBytes, long physicalBytes, double usageRatio)
        {
            uint flags = (uint)ErrorBits.CriticalMemoryPressure;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteCriticalMemoryPressureTelemetry(reservedBytes, physicalBytes, usageRatio);
            instance.TryExportSnapshot(
                ExportReason.CriticalMemoryPressure,
                flags,
                bypassCooldown: true);
        }

        /// <summary>
        /// Records a single-frame runtime allocation spike and exports a black-box snapshot.
        /// </summary>
        public static void ReportRuntimeMemorySpike(
            long previousBytes,
            long currentBytes,
            long deltaBytes,
            uint contextHash)
        {
            uint flags = (uint)ErrorBits.RuntimeMemorySpike;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteRuntimeMemorySpikeTelemetry(previousBytes, currentBytes, deltaBytes, contextHash);
            instance.TryExportSnapshot(
                ExportReason.RuntimeMemorySpike,
                flags,
                bypassCooldown: false);
        }

        /// <summary>
        /// Records a player physiology peak-stress event into the fixed black-box ring.
        /// </summary>
        public static void ReportPeakStressEvent(float stress01, float o2DrainMultiplier, uint peakEventCount)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WritePeakStressEventTelemetry(stress01, o2DrainMultiplier, peakEventCount);
        }

        /// <summary>
        /// Records non-finite player physiology state and queues a black-box export.
        /// </summary>
        public static void ReportPhysiologyNan(float stress01, float o2DrainMultiplier, uint contextHash)
        {
            uint flags = (uint)ErrorBits.PhysiologyNan;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WritePhysiologyNanTelemetry(stress01, o2DrainMultiplier, contextHash);
            instance.TryExportSnapshot(
                ExportReason.PhysiologyNan,
                flags,
                bypassCooldown: true);
        }

        /// <summary>
        /// Reports a dropped event payload caused by recursive cascade protection.
        /// </summary>
        public static void ReportEventCascadeWarning()
        {
            OrRuntimeFaultFlags((int)ErrorBits.EventCascadeWarning);
        }

        /// <summary>
        /// Reports a fixed-step catch-up clamp caused by a frame hitch.
        /// </summary>
        public static void ReportTemporalCompression()
        {
            OrRuntimeFaultFlags((int)ErrorBits.TemporalCompression);
        }

        /// <summary>
        /// Records dispatcher time-dilation state and tick overhead into the black-box ring.
        /// </summary>
        public static void ReportTimeDilationState(float scalar, double tickOverheadMilliseconds)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteTimeDilationTelemetry(scalar, tickOverheadMilliseconds);
        }

        /// <summary>
        /// Records simulation bucketing cadence and load state into the crash black-box ring.
        /// </summary>
        public static void ReportSimulationBucketFrame(SimulationBucketFrameState frameState)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteSimulationBucketTelemetry(frameState);
        }

        /// <summary>
        /// Reports a NativeQueue event bus that exceeded its frame budget for consecutive frames.
        /// </summary>
        /// <param name="queueHash">Stable queue identifier hash.</param>
        /// <param name="pendingCount">Pending payload count at the congestion point.</param>
        /// <param name="entityCount">Current spatial entity count.</param>
        public static void ReportBusCongestionWarning(uint queueHash, int pendingCount, int entityCount)
        {
            OrRuntimeFaultFlags((int)ErrorBits.BusCongestionWarning);
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteBusCongestionTelemetry(queueHash, pendingCount, entityCount);
        }

        /// <summary>
        /// Records typed signal-lane counts into the black-box ring.
        /// </summary>
        /// <param name="laneHash">Stable lane identifier hash.</param>
        /// <param name="queuedBeforeFlush">Payloads pending before the pre-simulation flush.</param>
        /// <param name="snapshotCount">Payloads copied into the contiguous frame snapshot.</param>
        /// <param name="droppedCount">Payloads discarded by lane overflow protection.</param>
        public static void ReportSignalLaneStats(uint laneHash, int queuedBeforeFlush, int snapshotCount, int droppedCount)
        {
            if (droppedCount > 0)
                OrRuntimeFaultFlags((int)ErrorBits.BusCongestionWarning);

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteSignalLaneTelemetry(laneHash, queuedBeforeFlush, snapshotCount, droppedCount);
        }

        /// <summary>
        /// Records the latest deterministic input frame into the crash black-box ring.
        /// </summary>
        public static void ReportDeterministicInputFrame(uint frame, uint sequence, uint buttonsBitmask, uint packedAxes)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteDeterministicInputTelemetry(frame, sequence, buttonsBitmask, packedAxes);
        }

        /// <summary>
        /// Records a failed owner-specific black-box export without allocating managed exception text.
        /// </summary>
        public static void ReportBlackBoxExportFailure()
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null)
            {
                OrRuntimeFaultFlags((int)ErrorBits.BlackBoxExportFault);
                return;
            }

            instance.RecordBlackBoxExportFailure();
        }

        /// <summary>
        /// Records active hull dent presentation state into the black-box ring.
        /// </summary>
        public static void ReportHullDentState(uint dentBufferHash, int activeHullDents, uint packedFlags)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteHullDentTelemetry(dentBufferHash, activeHullDents, packedFlags);
        }

        /// <summary>
        /// Records active octahedral impostor count into the black-box ring.
        /// </summary>
        public static void ReportActiveImpostors(int activeImpostors, int qualityFlags, uint contextHash)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteActiveImpostorTelemetry(activeImpostors, qualityFlags, contextHash);
        }

        /// <summary>
        /// Records one denied job admission into the scheduler black-box lane.
        /// </summary>
        public static void ReportJobAdmissionState(byte lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, int criticalDebtFrames, byte flags)
        {
            OrRuntimeFaultFlags(unchecked((int)ErrorBits.JobAdmissionStarvation));
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteJobAdmissionTelemetry(lane, jobHash, estimatedCostMs, remainingBudgetMs, criticalDebtFrames, flags);
        }

        /// <summary>
        /// Records periodic finite lane state for scheduler debt diagnostics.
        /// </summary>
        public static void ReportJobAdmissionLaneState(byte lane, float budgetMs, float refillMs, int criticalDebtFrames, uint killSwitchMask)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteJobAdmissionLaneTelemetry(lane, budgetMs, refillMs, criticalDebtFrames, killSwitchMask);
        }

        /// <summary>
        /// Records one scheduler EWMA cost-table slot before a forced fault export.
        /// </summary>
        public static void ReportJobAdmissionCostState(int slotIndex, uint jobHash, float ewmaCostMs, int costSlotCount, float overflowEwmaCostMs)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteJobAdmissionCostTelemetry(slotIndex, jobHash, ewmaCostMs, costSlotCount, overflowEwmaCostMs);
        }

        /// <summary>
        /// Records non-finite scheduler state and forces an immediate black-box export.
        /// </summary>
        public static void ReportJobAdmissionNonFinite(byte lane, uint jobHash, float value)
        {
            uint flags = (uint)ErrorBits.JobAdmissionStarvation;
            OrRuntimeFaultFlags(unchecked((int)flags));
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteJobAdmissionTelemetry(lane, jobHash, 0f, value, 0, 2);
            instance.TryExportSnapshot(ExportReason.JobAdmissionStarvation, flags, bypassCooldown: true);
        }

        /// <summary>
        /// Reports a dropped payload caused by the hard managed-event recursion circuit breaker.
        /// </summary>
        public static void ReportRecursiveCascadeCritical()
        {
            OrRuntimeFaultFlags((int)ErrorBits.EventCascadeWarning);
        }

        /// <summary>
        /// Records a rate-limited floating-origin shift vector sample into the telemetry ring.
        /// </summary>
        /// <param name="shiftOffset">Runtime-space shift vector applied this frame.</param>
        /// <param name="shiftSequence">Committed floating-origin sequence.</param>
        public static void ReportOriginShift(Vector3 shiftOffset, uint shiftSequence)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteOriginShiftTelemetry(shiftOffset, shiftSequence);
        }

        /// <summary>
        /// Records a high-G physics anomaly into the telemetry ring.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space body position where the anomaly was observed.</param>
        /// <param name="deltaVelocity">Velocity delta measured across the fixed step.</param>
        /// <param name="accelerationMetersPerSecondSq">Resolved acceleration magnitude.</param>
        public static void ReportKineticAnomaly(Vector3 runtimePosition, Vector3 deltaVelocity, float accelerationMetersPerSecondSq)
        {
            OrRuntimeFaultFlags((int)ErrorBits.KineticAnomaly);
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteKineticAnomalyTelemetry(runtimePosition, deltaVelocity, accelerationMetersPerSecondSq);
        }

        /// <summary>
        /// Records dispatcher load shedding when late-frame event queues exceed their time budget.
        /// </summary>
        public static void ReportLateFrameLoadShedding(uint queueHash, int remainingDispatchBudget)
        {
            OrRuntimeFaultFlags((int)ErrorBits.LateFrameLoadShedding);
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteLateFrameLoadSheddingTelemetry(queueHash, remainingDispatchBudget);
        }

        /// <summary>
        /// Records a single-lane dispatcher spike with the hashed managed stack context that observed it.
        /// </summary>
        public static void ReportCriticalPerformanceSpike(uint laneHash, double elapsedMilliseconds, uint stackHash)
        {
            uint flags = (uint)ErrorBits.CriticalPerformanceSpike;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteCriticalPerformanceSpikeTelemetry(laneHash, elapsedMilliseconds, stackHash);
            instance.TryExportSnapshot(
                ExportReason.CriticalPerformanceSpike,
                flags,
                bypassCooldown: false);
        }

        /// <summary>
        /// Records an input-to-screen latency debt marker with pending Awaitable debt as a packed numeric payload.
        /// </summary>
        public static void ReportLatencyCrime(int pendingContinuationCount, float latencyMs)
        {
            uint flags = (uint)ErrorBits.LatencyCrime;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteLatencyCrimeTelemetry(pendingContinuationCount, latencyMs);
            instance.TryExportSnapshot(
                ExportReason.LatencyCrime,
                flags,
                bypassCooldown: false);
        }

        /// <summary>
        /// Records world-streaming IO backpressure scalars into the fixed crash telemetry ring.
        /// </summary>
        public static void ReportStreamingBackpressureFrame(float debt01, double latencyEwmaMs, double oldestPendingMs, int pendingLoads)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteStreamingBackpressureTelemetry(debt01, latencyEwmaMs, oldestPendingMs, pendingLoads);
        }

        /// <summary>
        /// Records a persistent-native-buffer reallocation churn marker without retaining managed stack strings.
        /// </summary>
        public static void ReportNativeFragmentationRisk(uint allocationHash, int reallocationCount, long bytes)
        {
            uint flags = (uint)ErrorBits.NativeFragmentationRisk;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteNativeFragmentationRiskTelemetry(allocationHash, reallocationCount, bytes);
            instance.TryExportSnapshot(
                ExportReason.NativeFragmentationRisk,
                flags,
                bypassCooldown: false);
        }

        /// <summary>
        /// Records a TempJob native buffer that survived past the four-frame legal window.
        /// </summary>
        public static void ReportStaleBufferCrime(uint allocationHash, int retentionFrames, long bytes)
        {
            uint flags = (uint)ErrorBits.StaleBufferCrime;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteStaleBufferCrimeTelemetry(allocationHash, retentionFrames, bytes);
            instance.TryExportSnapshot(
                ExportReason.StaleBufferCrime,
                flags,
                bypassCooldown: false);
        }

        /// <summary>
        /// Records a Temp native buffer that survived past its legal one-frame window.
        /// </summary>
        public static void ReportNativeTransientLeak(uint allocationHash, int retentionFrames, long bytes)
        {
            uint flags = (uint)ErrorBits.NativeTransientLeak;
            OrRuntimeFaultFlags(unchecked((int)flags));

            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteNativeTransientLeakTelemetry(allocationHash, retentionFrames, bytes);
            instance.TryExportSnapshot(
                ExportReason.NativeTransientLeak,
                flags,
                bypassCooldown: false);
        }

        public static void ReportAudioOverflowDropWarning(int overflowDropCount, int bufferedFrames, int writableFrames)
        {
            OrRuntimeFaultFlags((int)ErrorBits.AudioOverflowDropWarning);
            Volatile.Write(ref _pendingAudioOverflowBufferedFrames, math.max(0, bufferedFrames));
            Volatile.Write(ref _pendingAudioOverflowWritableFrames, math.max(0, writableFrames));
            Interlocked.Exchange(ref _pendingAudioOverflowDropCount, math.max(1, overflowDropCount));
        }

        public static void ReportAudioDspStats(int activeDspVoices, int audioBufferUnderruns)
        {
            ReportAudioDspSpatializationFrame(activeDspVoices, 0, audioBufferUnderruns);
        }

        public static void ReportAudioDspSpatializationFrame(
            int activeDspVoices,
            int sdfSampleTimeMicroseconds,
            int audioBufferUnderruns)
        {
            Volatile.Write(ref _pendingActiveDspVoices, math.max(0, activeDspVoices));
            Volatile.Write(ref _pendingSdfSampleTimeMicroseconds, math.max(0, sdfSampleTimeMicroseconds));
            Volatile.Write(ref _pendingAudioBufferUnderruns, math.max(0, audioBufferUnderruns));
            Interlocked.Exchange(ref _pendingAudioDspStats, 1);
        }

        /// <summary>
        /// Records a bootstrap safe-halt forensic row and queues the current crash snapshot for the BLACKBOX worker.
        /// </summary>
        public static void ReportBootstrapSafeHalt(
            BootstrapStepToken activeStep,
            BootstrapStepToken longestStep,
            double bootElapsedSeconds,
            double activeStepElapsedMilliseconds,
            uint recentStepMaskLow,
            uint recentStepMaskHigh,
            uint recentStepHash0,
            uint recentStepHash1,
            uint recentStepHash2,
            uint recentStepHash3,
            uint recentStepHash4,
            uint recentStepHash5,
            uint recentStepHash6,
            uint recentStepHash7,
            uint recentStepHash8,
            uint recentStepHash9)
        {
            OrRuntimeFaultFlags((int)ErrorBits.BootstrapSafeHalt);
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteBootstrapSafeHaltTelemetry(
                activeStep,
                longestStep,
                bootElapsedSeconds,
                activeStepElapsedMilliseconds,
                recentStepMaskLow,
                recentStepMaskHigh);
            instance.TryExportSnapshot(
                ExportReason.BootstrapSafeHalt,
                (uint)ErrorBits.BootstrapSafeHalt,
                bypassCooldown: true);
        }

        /// <summary>
        /// Records a runtime watchdog stall before the watchdog terminates the process.
        /// </summary>
        public static void ReportRuntimeWatchdogStall(uint lane, uint counter)
        {
            OrRuntimeFaultFlags((int)ErrorBits.RuntimeWatchdogStall);
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteRuntimeWatchdogStallTelemetry(lane, counter);
            instance.TryExportSnapshot(
                ExportReason.RuntimeWatchdogStall,
                (uint)ErrorBits.RuntimeWatchdogStall,
                bypassCooldown: true);
        }

        /// <summary>
        /// Records an AUP/runtime coordinate resync applied after fixed simulation.
        /// </summary>
        public static void ReportAupJitterCorrection(Vector3 runtimePosition, float correctionMeters)
        {
            OrRuntimeFaultFlags((int)ErrorBits.AupJitterCorrection);
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteAupJitterCorrectionTelemetry(runtimePosition, correctionMeters);
        }

        /// <summary>
        /// Records the current watchdog maximum AUP/runtime drift without raising a crash export fault.
        /// </summary>
        public static void ReportAupMaxDriftError(Vector3 runtimePosition, float maxDriftErrorMeters)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteAupMaxDriftErrorTelemetry(runtimePosition, maxDriftErrorMeters);
        }

        /// <summary>
        /// Writes one bootstrap phase duration sample into the crash telemetry ring.
        /// </summary>
        /// <param name="step">Bootstrap phase token.</param>
        /// <param name="elapsedMilliseconds">Measured phase duration in milliseconds.</param>
        public static void RecordBootstrapPhaseDuration(BootstrapStepToken step, double elapsedMilliseconds)
        {
            CrashTelemetryBuffer instance = GlobalRegistry.CrashTelemetry;
            if (instance == null || !instance._ringBuffer.IsCreated || step == BootstrapStepToken.None)
                return;

            bool isPerfWarning = elapsedMilliseconds > BootstrapPerfWarningThresholdMilliseconds;
            if (isPerfWarning)
                OrRuntimeFaultFlags((int)ErrorBits.BootPerfWarning);

            instance.WriteBootstrapPhaseDuration(step, elapsedMilliseconds, isPerfWarning);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Copies the latest retained crash telemetry rows into an editor-owned destination list.
        /// </summary>
        /// <param name="destination">Editor-owned destination list.</param>
        /// <returns>Copied frame count.</returns>
        public int CopyEditorSnapshot(System.Collections.Generic.List<EditorSnapshotEntry> destination)
        {
            if (destination == null)
                return 0;

            destination.Clear();
            if (!_ringBuffer.IsCreated)
                return 0;

            long writeCursor = Volatile.Read(ref _writeCursor);
            long committedEntries = math.min(writeCursor, ExportSnapshotEntries);
            if (committedEntries <= 0)
                return 0;

            long startCursor = writeCursor - committedEntries;
            for (int i = 0; i < committedEntries; i++)
            {
                int ringIndex = (int)(startCursor + i) & RingCapacityMask;
                TelemetryEntry entry = _ringBuffer[ringIndex];
                destination.Add(new EditorSnapshotEntry(
                    entry.FrameIndex,
                    entry.SystemMask,
                    entry.DeltaTime,
                    entry.LatencyMs,
                    entry.GpuFrameTime,
                    entry.MemoryUsedMb,
                    new Vector3(entry.PlayerAup.x, entry.PlayerAup.y, entry.PlayerAup.z),
                    entry.ActiveChunkCount,
                    entry.VelocityPacked,
                    entry.GcAllocBytes,
                    entry.ErrorFlags,
                    entry.ExportReason));
            }

            return destination.Count;
        }
#endif

        private void Awake()
        {
            CrashTelemetryBuffer registeredInstance = GlobalRegistry.CrashTelemetry;
            if (registeredInstance != null && registeredInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeBuffers();
            ResolveProfilerRecorders();
        }

        private void Start()
        {
            if (!TryRegisterRuntimeService())
                return;

            CacheRegistryPresenceCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntimeService())
                return;

            CacheRegistryPresenceCold();
            TryRegisterHotSwapListener();
            Subscribe();
            TryRegister();
        }

        private void OnDisable()
        {
            Unsubscribe();
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeService();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            TryUnregister();
            TryUnregisterHotSwapListener();
            DisposeBuffers();
            TryUnregisterRuntimeService();
        }

        private void OnApplicationQuit()
        {
            uint stickyErrorFlags = unchecked((uint)Volatile.Read(ref _stickyErrorFlags));
            if (stickyErrorFlags != 0u)
                TryExportSnapshot(ExportReason.ApplicationQuit, stickyErrorFlags, bypassCooldown: true);

            DisposeBuffers();
        }

        /// <summary>
        /// Records one telemetry entry on the shared game tick.
        /// </summary>
        /// <param name="dt">Frame delta passed by <see cref="GameTickManager"/>.</param>
        public void Tick(float dt)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.CrashTelemetry);
#endif
            if (!_ringBuffer.IsCreated)
                return;

            using (ProfilerRegistry.TelemetryWrite.Auto())
            {
                ResolvePlayerTransform(dt);

                FrameTimingManager.CaptureFrameTimings();
                float gpuFrameTime = SampleGpuFrameTimeMs();
                float reservedMemoryMb = SampleReservedMemoryMegabytes();
                float3 playerAup = SamplePlayerPosition(out bool hasPlayer);
                uint systemMask = SampleSystemMask();
                uint activeChunkCount = SampleActiveChunkCount();
                uint playerVelocityPacked = SamplePlayerVelocityPacked();
                uint gcAllocBytes = unchecked((uint)math.max(0, ReadIntValue(_gcAllocRecorder)));
                uint errorFlags = BuildErrorFlags(dt, reservedMemoryMb, playerAup, hasPlayer);
                float latencyMs = SanitizeMilliseconds(InputLatencyTracker.SampleCompletedLatencyMs());
                int pendingAwaitableContinuations = AwaitableDebtMonitor.PendingNextFrameContinuations;
                int peakAwaitableContinuations = AwaitableDebtMonitor.ConsumePeakNextFrameContinuations();
                int awaitableDebtSample = math.max(pendingAwaitableContinuations, peakAwaitableContinuations);
                if (awaitableDebtSample > AwaitableDebtMonitor.LatencyCrimeThreshold)
                {
                    errorFlags |= (uint)ErrorBits.LatencyCrime;
                    systemMask |= (uint)SystemBits.Input;
                }

                AwaitableDebtMonitor.AuditLatencyDebt(awaitableDebtSample, latencyMs);
                OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
                uint threadedFaultFlags = unchecked((uint)Interlocked.Exchange(ref _threadedFaultFlags, 0));
                uint runtimeFaultFlags = unchecked((uint)Interlocked.Exchange(ref _runtimeFaultFlags, 0));
                if (threadedFaultFlags != 0u)
                    errorFlags |= threadedFaultFlags;
                if (runtimeFaultFlags != 0u)
                    errorFlags |= runtimeFaultFlags;
                if ((runtimeFaultFlags & ExportInternalFaultMask) != 0u)
                    systemMask |= (uint)SystemBits.Memory;

                int blackBoxExportFailureCount = Interlocked.Exchange(ref _pendingBlackBoxExportFailureCount, 0);
                if (blackBoxExportFailureCount > 0)
                {
                    WriteBlackBoxExportFaultTelemetry(blackBoxExportFailureCount);
                    errorFlags |= (uint)ErrorBits.BlackBoxExportFault;
                    systemMask |= (uint)SystemBits.Memory;
                }

                int blackBoxExportDroppedCount = Interlocked.Exchange(ref _pendingBlackBoxExportDroppedCount, 0);
                if (blackBoxExportDroppedCount > 0)
                {
                    WriteBlackBoxExportDroppedTelemetry(blackBoxExportDroppedCount);
                    errorFlags |= (uint)ErrorBits.BlackBoxExportDropped;
                    systemMask |= (uint)SystemBits.Memory;
                }

                int blackBoxExportSuppressedCount = Interlocked.Exchange(ref _pendingBlackBoxExportSuppressedCount, 0);
                if (blackBoxExportSuppressedCount > 0)
                {
                    WriteBlackBoxExportSuppressedTelemetry(blackBoxExportSuppressedCount);
                    errorFlags |= (uint)ErrorBits.BlackBoxExportSuppressed;
                    systemMask |= (uint)SystemBits.Memory;
                }

                int audioOverflowDropCount = Interlocked.Exchange(ref _pendingAudioOverflowDropCount, 0);
                if (audioOverflowDropCount > 0)
                {
                    WriteAudioOverflowDropTelemetry(
                        audioOverflowDropCount,
                        Volatile.Read(ref _pendingAudioOverflowBufferedFrames),
                        Volatile.Read(ref _pendingAudioOverflowWritableFrames));
                    PublishPerformanceWarningNoThrow(
                        _audioOverflowDropWarningHash,
                        _audioOverflowBufferContextHash,
                        audioOverflowDropCount);
                }

                if (Interlocked.Exchange(ref _pendingAudioDspStats, 0) != 0)
                {
                    WriteAudioDspStatsTelemetry(
                        Volatile.Read(ref _pendingActiveDspVoices),
                        Volatile.Read(ref _pendingSdfSampleTimeMicroseconds),
                        Volatile.Read(ref _pendingAudioBufferUnderruns));
                }

                uint frameIndex = unchecked((uint)Time.frameCount);
                int writeIndex = ReserveTelemetryWriteIndex();

                TelemetryEntry entry = default;
                entry.FrameIndex = frameIndex;
                entry.SystemMask = systemMask;
                entry.DeltaTime = dt;
                _lastLatencyMs = latencyMs;
                entry.LatencyMs = latencyMs;
                entry.GpuFrameTime = gpuFrameTime;
                entry.MemoryUsedMb = reservedMemoryMb;
                entry.PlayerAup = playerAup;
                entry.ActiveChunkCount = activeChunkCount;
                entry.ErrorFlags = errorFlags;
                entry.ExportReason = (uint)ExportReason.None;
                entry.AupShiftSequence = shiftEvent.Sequence;
                entry.VelocityPacked = playerVelocityPacked;
                entry.GcAllocBytes = gcAllocBytes;
                entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
                _ringBuffer[writeIndex] = entry;
                TryWriteLiveTelemetry(frameIndex, dt, reservedMemoryMb, activeChunkCount);

                if (errorFlags != 0u)
                {
                    OrStickyErrorFlags(errorFlags);
                    uint exportableErrorFlags = errorFlags & ~ExportInternalFaultMask;
                    if (exportableErrorFlags == 0u)
                        return;

                    ExportReason exportReason = SelectExportReason(exportableErrorFlags);
                    TryExportSnapshot(exportReason, errorFlags, bypassCooldown: false);
                }
            }
        }

        private static ExportReason SelectExportReason(uint errorFlags)
        {
            if ((errorFlags & (uint)ErrorBits.CriticalPerformanceSpike) != 0u)
                return ExportReason.CriticalPerformanceSpike;
            if ((errorFlags & (uint)ErrorBits.LatencyCrime) != 0u)
                return ExportReason.LatencyCrime;
            if ((errorFlags & (uint)ErrorBits.NativeFragmentationRisk) != 0u)
                return ExportReason.NativeFragmentationRisk;
            if ((errorFlags & (uint)ErrorBits.StaleBufferCrime) != 0u)
                return ExportReason.StaleBufferCrime;
            if ((errorFlags & (uint)ErrorBits.NativeTransientLeak) != 0u)
                return ExportReason.NativeTransientLeak;
            if ((errorFlags & (uint)ErrorBits.RuntimeMemorySpike) != 0u)
                return ExportReason.RuntimeMemorySpike;
            if ((errorFlags & (uint)ErrorBits.PhysiologyNan) != 0u)
                return ExportReason.PhysiologyNan;
            if ((errorFlags & (uint)ErrorBits.JobAdmissionStarvation) != 0u)
                return ExportReason.JobAdmissionStarvation;
            if ((errorFlags & (uint)ErrorBits.CriticalMemoryPressure) != 0u)
                return ExportReason.CriticalMemoryPressure;
            if ((errorFlags & (uint)ErrorBits.RuntimeWatchdogStall) != 0u)
                return ExportReason.RuntimeWatchdogStall;
            if ((errorFlags & (uint)ErrorBits.AupJitterCorrection) != 0u)
                return ExportReason.AupJitterCorrection;
            if ((errorFlags & (uint)ErrorBits.BootstrapSafeHalt) != 0u)
                return ExportReason.BootstrapSafeHalt;

            return ExportReason.ErrorFlags;
        }

        /// <summary>
        /// Receives the fixed-step tick; the 64-byte black box keeps this slot for input latency.
        /// </summary>
        /// <param name="fdt">Fixed delta passed by <see cref="GameTickManager"/>.</param>
        public void FixedTick(float fdt)
        {
            _ = fdt;
        }

        private static float SampleReservedMemoryMegabytes()
        {
            return Profiler.GetTotalReservedMemoryLong() * BytesToMegabytes;
        }

        private int ReserveTelemetryWriteIndex()
        {
            long cursor = Interlocked.Increment(ref _writeCursor) - 1L;
            return (int)cursor & RingCapacityMask;
        }

        private void WriteBootstrapPhaseDuration(BootstrapStepToken step, double elapsedMilliseconds, bool isPerfWarning)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Bootstrap;
            entry.DeltaTime = 0f;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = (float)elapsedMilliseconds;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = float3.zero;
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = isPerfWarning ? (uint)ErrorBits.BootPerfWarning : 0u;
            entry.ExportReason = isPerfWarning
                ? (uint)ExportReason.BootPerfWarning
                : (uint)ExportReason.BootstrapPhaseDuration;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackBootstrapPhaseDuration(step, elapsedMilliseconds);
            entry.SubsystemHeatPacked = isPerfWarning ? _bootPerfWarningHash : 0u;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteOriginShiftTelemetry(Vector3 shiftOffset, uint shiftSequence)
        {
            float now = Time.unscaledTime;
            if (now < _nextOriginShiftTelemetryTime)
                return;

            float3 shift3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(shift3)))
                return;

            _nextOriginShiftTelemetryTime = now + OriginShiftTelemetryIntervalSeconds;
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.OriginShift;
            entry.DeltaTime = 0f;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = shift3;
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftSequence;
            entry.AiStatePacked = PackAiState();
            entry.SubsystemHeatPacked = PackSubsystemHeat();
            entry.LastOriginShiftFrame = frameIndex;
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteBusCongestionTelemetry(uint queueHash, int pendingCount, int entityCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = (float)Time.unscaledTimeAsDouble;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, entityCount));
            entry.ErrorFlags = (uint)ErrorBits.BusCongestionWarning;
            entry.ExportReason = (uint)ExportReason.BusCongestionWarning;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = queueHash;
            entry.SubsystemHeatPacked = unchecked((uint)math.max(0, pendingCount));
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
            TryExportSnapshot(ExportReason.BusCongestionWarning, (uint)ErrorBits.BusCongestionWarning, bypassCooldown: false);
        }

        private void WriteSignalLaneTelemetry(uint laneHash, int queuedBeforeFlush, int snapshotCount, int droppedCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            uint errorFlags = droppedCount > 0 ? (uint)ErrorBits.BusCongestionWarning : 0u;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)Math.Max(0, queuedBeforeFlush));
            entry.ErrorFlags = errorFlags;
            entry.ExportReason = errorFlags != 0u ? (uint)ExportReason.BusCongestionWarning : (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = laneHash;
            entry.SubsystemHeatPacked = PackSignalLaneCounts(snapshotCount, droppedCount);
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;

            if (errorFlags != 0u)
                TryExportSnapshot(ExportReason.BusCongestionWarning, errorFlags, bypassCooldown: false);
        }

        private void WriteDeterministicInputTelemetry(uint frame, uint sequence, uint buttonsBitmask, uint packedAxes)
        {
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frame;
            entry.SystemMask = (uint)SystemBits.Input;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = sequence;
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = buttonsBitmask;
            entry.SubsystemHeatPacked = packedAxes;
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteHullDentTelemetry(uint dentBufferHash, int activeHullDents, uint packedFlags)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Vfx;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)Math.Max(0, activeHullDents));
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = dentBufferHash;
            entry.SubsystemHeatPacked = packedFlags;
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private static uint PackSignalLaneCounts(int snapshotCount, int droppedCount)
        {
            uint snapshot = unchecked((uint)Math.Min(Math.Max(0, snapshotCount), ushort.MaxValue));
            uint dropped = unchecked((uint)Math.Min(Math.Max(0, droppedCount), ushort.MaxValue));
            return snapshot | (dropped << 16);
        }

        private static uint PackJobAdmissionState(int criticalDebtFrames, byte flags)
        {
            uint debt = unchecked((uint)Math.Min(Math.Max(0, criticalDebtFrames), 0x00FFFFFF));
            return debt | ((uint)flags << 24);
        }

        private static uint PackSimulationBuckets(int activeFastBucket, int activeSlowBucket, int activeColdBucket, byte activeSlowBucketCount)
        {
            uint fast = unchecked((uint)Math.Min(Math.Max(0, activeFastBucket), byte.MaxValue));
            uint slow = unchecked((uint)Math.Min(Math.Max(0, activeSlowBucket), byte.MaxValue));
            uint cold = unchecked((uint)Math.Min(Math.Max(0, activeColdBucket), byte.MaxValue));
            uint count = activeSlowBucketCount;
            return fast | (slow << 8) | (cold << 16) | (count << 24);
        }

        private static uint PackSimulationBucketCadence(int slowBucketCount, int slowBucketMask, int criticalDebtFrames, byte aupBarrierActive)
        {
            uint count = unchecked((uint)Math.Min(Math.Max(0, slowBucketCount), 1023));
            uint mask = unchecked((uint)Math.Min(Math.Max(0, slowBucketMask), 1023));
            uint debt = unchecked((uint)Math.Min(Math.Max(0, criticalDebtFrames), 1023));
            uint barrier = aupBarrierActive != 0 ? 1u << 31 : 0u;
            return count | (mask << 10) | (debt << 20) | barrier;
        }

        private void WriteJobAdmissionTelemetry(byte lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, int criticalDebtFrames, byte flags)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Scheduler;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = estimatedCostMs;
            entry.GpuFrameTime = remainingBudgetMs;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = lane;
            entry.ErrorFlags = (uint)ErrorBits.JobAdmissionStarvation;
            entry.ExportReason = (uint)ExportReason.JobAdmissionStarvation;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = jobHash;
            entry.SubsystemHeatPacked = PackJobAdmissionState(criticalDebtFrames, flags);
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteJobAdmissionLaneTelemetry(byte lane, float budgetMs, float refillMs, int criticalDebtFrames, uint killSwitchMask)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Scheduler;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = budgetMs;
            entry.GpuFrameTime = refillMs;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = lane;
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = killSwitchMask;
            entry.SubsystemHeatPacked = PackJobAdmissionState(criticalDebtFrames, 0);
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteJobAdmissionCostTelemetry(int slotIndex, uint jobHash, float ewmaCostMs, int costSlotCount, float overflowEwmaCostMs)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Scheduler;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = ewmaCostMs;
            entry.GpuFrameTime = overflowEwmaCostMs;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)Math.Max(0, slotIndex));
            entry.ErrorFlags = (uint)ErrorBits.JobAdmissionStarvation;
            entry.ExportReason = (uint)ExportReason.JobAdmissionStarvation;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = jobHash;
            entry.SubsystemHeatPacked = unchecked((uint)Math.Max(0, costSlotCount));
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteKineticAnomalyTelemetry(Vector3 runtimePosition, Vector3 deltaVelocity, float accelerationMetersPerSecondSq)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float3 absolutePosition = ToAbsoluteUniversePosition(runtimePosition);
            float3 deltaVelocity3 = new float3(deltaVelocity.x, deltaVelocity.y, deltaVelocity.z);
            if (!math.all(math.isfinite(absolutePosition)) || !math.all(math.isfinite(deltaVelocity3)))
                return;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physics;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = math.max(0f, accelerationMetersPerSecondSq);
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = absolutePosition;
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.KineticAnomaly;
            entry.ExportReason = (uint)ExportReason.KineticAnomaly;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackSignedVectorComponent(deltaVelocity3.x) |
                                  (PackSignedVectorComponent(deltaVelocity3.y) << 10) |
                                  (PackSignedVectorComponent(deltaVelocity3.z) << 20);
            entry.SubsystemHeatPacked = PackSubsystemHeat();
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteActiveImpostorTelemetry(int activeImpostors, int qualityFlags, uint contextHash)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Rendering | (uint)SystemBits.WorldStreaming;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, activeImpostors));
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, qualityFlags));
            entry.SubsystemHeatPacked = contextHash;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteNanPhysicsRecoveryTelemetry(Vector3 invalidRuntimePosition, Vector3 recoveredRuntimePosition)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float3 recoveredAup = ToAbsoluteUniversePosition(recoveredRuntimePosition);
            if (!math.all(math.isfinite(recoveredAup)))
                recoveredAup = float3.zero;

            float3 invalidPosition = new float3(invalidRuntimePosition.x, invalidRuntimePosition.y, invalidRuntimePosition.z);
            bool3 invalidFinite = math.isfinite(invalidPosition);
            float3 finiteInvalidPayload = math.select(float3.zero, invalidPosition, invalidFinite);
            uint invalidComponentMask = (invalidFinite.x ? 0u : 1u) |
                                        (invalidFinite.y ? 0u : 2u) |
                                        (invalidFinite.z ? 0u : 4u);

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physics | (uint)SystemBits.OriginShift;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = recoveredAup;
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.NanPhysics;
            entry.ExportReason = (uint)ExportReason.ErrorFlags;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackSignedVectorComponent(finiteInvalidPayload.x) |
                                  (PackSignedVectorComponent(finiteInvalidPayload.y) << 10) |
                                  (PackSignedVectorComponent(finiteInvalidPayload.z) << 20);
            entry.SubsystemHeatPacked = invalidComponentMask;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteLateFrameLoadSheddingTelemetry(uint queueHash, int remainingDispatchBudget)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = (float)Time.unscaledTimeAsDouble;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.LateFrameLoadShedding;
            entry.ExportReason = (uint)ExportReason.LateFrameLoadShedding;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = queueHash;
            entry.SubsystemHeatPacked = unchecked((uint)math.max(0, remainingDispatchBudget));
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteSimulationBucketTelemetry(SimulationBucketFrameState frameState)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float activeLoadMs = math.isfinite(frameState.ActiveBucketLoadMs)
                ? math.max(0f, frameState.ActiveBucketLoadMs)
                : 0f;
            float jitterVarianceMs = math.isfinite(frameState.JitterVarianceMs)
                ? math.max(0f, frameState.JitterVarianceMs)
                : 0f;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Scheduler;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = activeLoadMs;
            entry.GpuFrameTime = jitterVarianceMs;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)Math.Max(0, frameState.CurrentFrameCount));
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackSimulationBuckets(
                frameState.ActiveFastBucket,
                frameState.ActiveSlowBucket,
                frameState.ActiveColdBucket,
                frameState.ActiveSlowBucketCount);
            entry.SubsystemHeatPacked = PackSimulationBucketCadence(
                frameState.SlowBucketCount,
                frameState.SlowBucketMask,
                frameState.CriticalDebtFrames,
                frameState.AupBarrierActive);
            entry.LastOriginShiftFrame = unchecked((uint)Math.Max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteCriticalPerformanceSpikeTelemetry(uint laneHash, double elapsedMilliseconds, uint stackHash)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = elapsedMilliseconds > float.MaxValue
                ? float.MaxValue
                : (float)math.max(0d, elapsedMilliseconds);
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.CriticalPerformanceSpike;
            entry.ExportReason = (uint)ExportReason.CriticalPerformanceSpike;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = laneHash;
            entry.SubsystemHeatPacked = stackHash;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteLatencyCrimeTelemetry(int pendingContinuationCount, float latencyMs)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Input | (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            latencyMs = SanitizeMilliseconds(latencyMs);
            entry.LatencyMs = latencyMs;
            entry.GpuFrameTime = ReadMilliseconds(_frameTimeRecorder);
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, pendingContinuationCount));
            entry.ErrorFlags = (uint)ErrorBits.LatencyCrime;
            entry.ExportReason = (uint)ExportReason.LatencyCrime;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, pendingContinuationCount));
            entry.SubsystemHeatPacked = PackFloatToMilliseconds(latencyMs);
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteStreamingBackpressureTelemetry(float debt01, double latencyEwmaMs, double oldestPendingMs, int pendingLoads)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.WorldStreaming;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            float latencyMs = math.isfinite((float)latencyEwmaMs) ? (float)math.max(0d, latencyEwmaMs) : 0f;
            float pendingMs = math.isfinite((float)oldestPendingMs) ? (float)math.max(0d, oldestPendingMs) : 0f;
            entry.LatencyMs = latencyMs;
            entry.GpuFrameTime = pendingMs;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, pendingLoads));
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackFloatToMilliseconds(pendingMs);
            entry.SubsystemHeatPacked = unchecked((uint)math.round(math.saturate(debt01) * 1000f));
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteNativeFragmentationRiskTelemetry(uint allocationHash, int reallocationCount, long bytes)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, reallocationCount));
            entry.ErrorFlags = (uint)ErrorBits.NativeFragmentationRisk;
            entry.ExportReason = (uint)ExportReason.NativeFragmentationRisk;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = allocationHash;
            entry.SubsystemHeatPacked = PackBytesToMegabytes(bytes);
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteStaleBufferCrimeTelemetry(uint allocationHash, int retentionFrames, long bytes)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Memory;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, retentionFrames));
            entry.ErrorFlags = (uint)ErrorBits.StaleBufferCrime;
            entry.ExportReason = (uint)ExportReason.StaleBufferCrime;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = allocationHash;
            entry.SubsystemHeatPacked = PackBytesToMegabytes(bytes);
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteNativeTransientLeakTelemetry(uint allocationHash, int retentionFrames, long bytes)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Memory;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, retentionFrames));
            entry.ErrorFlags = (uint)ErrorBits.NativeTransientLeak;
            entry.ExportReason = (uint)ExportReason.NativeTransientLeak;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = allocationHash;
            entry.SubsystemHeatPacked = PackBytesToMegabytes(bytes);
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteBlackBoxExportFaultTelemetry(int failureCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Memory;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, failureCount));
            entry.ErrorFlags = (uint)ErrorBits.BlackBoxExportFault;
            entry.ExportReason = (uint)ExportReason.BlackBoxExportFault;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, failureCount));
            entry.SubsystemHeatPacked = unchecked((uint)GetCrashSafeExportFrame());
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteBlackBoxExportDroppedTelemetry(int droppedCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Memory;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, droppedCount));
            entry.ErrorFlags = (uint)ErrorBits.BlackBoxExportDropped;
            entry.ExportReason = (uint)ExportReason.BlackBoxExportDropped;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, droppedCount));
            entry.SubsystemHeatPacked = unchecked((uint)GetCrashSafeExportFrame());
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteBlackBoxExportSuppressedTelemetry(int suppressedCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Memory;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, suppressedCount));
            entry.ErrorFlags = (uint)ErrorBits.BlackBoxExportSuppressed;
            entry.ExportReason = (uint)ExportReason.BlackBoxExportSuppressed;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, suppressedCount));
            entry.SubsystemHeatPacked = unchecked((uint)GetCrashSafeExportFrame());
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteAudioOverflowDropTelemetry(int overflowDropCount, int bufferedFrames, int writableFrames)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Audio;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = (float)Time.unscaledTimeAsDouble;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, bufferedFrames));
            entry.ErrorFlags = (uint)ErrorBits.AudioOverflowDropWarning;
            entry.ExportReason = (uint)ExportReason.AudioOverflowDropWarning;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, overflowDropCount));
            entry.SubsystemHeatPacked = unchecked((uint)math.max(0, writableFrames));
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteAudioDspStatsTelemetry(
            int activeDspVoices,
            int sdfSampleTimeMicroseconds,
            int audioBufferUnderruns)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Audio | (uint)SystemBits.Voxel;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = math.max(0, sdfSampleTimeMicroseconds);
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, activeDspVoices));
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = unchecked((uint)math.max(0, audioBufferUnderruns));
            entry.SubsystemHeatPacked = unchecked((uint)math.max(0, sdfSampleTimeMicroseconds));
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteBootstrapSafeHaltTelemetry(
            BootstrapStepToken activeStep,
            BootstrapStepToken longestStep,
            double bootElapsedSeconds,
            double activeStepElapsedMilliseconds,
            uint recentStepMaskLow,
            uint recentStepMaskHigh)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Bootstrap;
            entry.DeltaTime = (float)math.max(0d, bootElapsedSeconds);
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = (float)math.max(0d, activeStepElapsedMilliseconds);
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = new float3((int)activeStep, (int)longestStep, (float)math.max(0d, LongestStepMillisecondsSafe()));
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.BootstrapSafeHalt;
            entry.ExportReason = (uint)ExportReason.BootstrapSafeHalt;
            entry.AupShiftSequence = HectonFloatingOrigin.LastShiftEvent.Sequence;
            entry.AiStatePacked = recentStepMaskLow;
            entry.SubsystemHeatPacked = recentStepMaskHigh;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, HectonFloatingOrigin.LastShiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteRuntimeWatchdogStallTelemetry(uint lane, uint counter)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Bootstrap;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = (float)Time.unscaledTimeAsDouble;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.RuntimeWatchdogStall;
            entry.ExportReason = (uint)ExportReason.RuntimeWatchdogStall;
            entry.AupShiftSequence = HectonFloatingOrigin.LastShiftEvent.Sequence;
            entry.AiStatePacked = lane;
            entry.SubsystemHeatPacked = counter;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, HectonFloatingOrigin.LastShiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteCriticalMemoryPressureTelemetry(long reservedBytes, long physicalBytes, double usageRatio)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = usageRatio > float.MaxValue ? float.MaxValue : (float)usageRatio;
            entry.MemoryUsedMb = reservedBytes * BytesToMegabytes;
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.CriticalMemoryPressure;
            entry.ExportReason = (uint)ExportReason.CriticalMemoryPressure;
            entry.AupShiftSequence = HectonFloatingOrigin.LastShiftEvent.Sequence;
            entry.AiStatePacked = PackBytesToMegabytes(reservedBytes);
            entry.SubsystemHeatPacked = PackBytesToMegabytes(physicalBytes);
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, HectonFloatingOrigin.LastShiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteRuntimeMemorySpikeTelemetry(
            long previousBytes,
            long currentBytes,
            long deltaBytes,
            uint contextHash)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Memory;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = deltaBytes * BytesToMegabytes;
            entry.MemoryUsedMb = currentBytes * BytesToMegabytes;
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.RuntimeMemorySpike;
            entry.ExportReason = (uint)ExportReason.RuntimeMemorySpike;
            entry.AupShiftSequence = HectonFloatingOrigin.LastShiftEvent.Sequence;
            entry.AiStatePacked = PackBytesToMegabytes(previousBytes);
            entry.SubsystemHeatPacked = contextHash;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, HectonFloatingOrigin.LastShiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WritePeakStressEventTelemetry(float stress01, float o2DrainMultiplier, uint peakEventCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float safeStress = math.isfinite(stress01) ? math.saturate(stress01) : 0f;
            float safeO2Drain = math.isfinite(o2DrainMultiplier) ? math.saturate(o2DrainMultiplier * 0.4f) : 0f;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physiology;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = safeStress;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.PeakStressEvent;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackTwoUnitFloats(safeStress, safeO2Drain);
            entry.SubsystemHeatPacked = peakEventCount;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WritePhysiologyNanTelemetry(float stress01, float o2DrainMultiplier, uint contextHash)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float safeStress = math.isfinite(stress01) ? math.saturate(stress01) : 0f;
            float safeO2Drain = math.isfinite(o2DrainMultiplier) ? math.saturate(o2DrainMultiplier * 0.4f) : 0f;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physiology;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = safeStress;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.PhysiologyNan;
            entry.ExportReason = (uint)ExportReason.PhysiologyNan;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackTwoUnitFloats(safeStress, safeO2Drain);
            entry.SubsystemHeatPacked = contextHash;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteAupJitterCorrectionTelemetry(Vector3 runtimePosition, float correctionMeters)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physics;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = math.max(0f, correctionMeters);
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = ToAbsoluteUniversePosition(runtimePosition);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.AupJitterCorrection;
            entry.ExportReason = (uint)ExportReason.AupJitterCorrection;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackAiState();
            entry.SubsystemHeatPacked = PackSubsystemHeat();
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteAupMaxDriftErrorTelemetry(Vector3 runtimePosition, float maxDriftErrorMeters)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float safeMaxDriftError = math.isfinite(maxDriftErrorMeters) ? math.max(0f, maxDriftErrorMeters) : float.MaxValue;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physics | (uint)SystemBits.OriginShift;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = safeMaxDriftError;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = ToAbsoluteUniversePosition(runtimePosition);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = (uint)ErrorBits.None;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackAiState();
            entry.SubsystemHeatPacked = PackSubsystemHeat();
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void WriteTimeDilationTelemetry(float scalar, double tickOverheadMilliseconds)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float safeScalar = math.isfinite(scalar) ? math.max(0f, scalar) : 1f;
            float safeOverhead = tickOverheadMilliseconds > float.MaxValue
                ? float.MaxValue
                : SanitizeMilliseconds((float)tickOverheadMilliseconds);

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.TimeDilation;
            entry.DeltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            entry.LatencyMs = safeOverhead;
            entry.GpuFrameTime = safeScalar;
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackTwoUnitFloats(math.saturate(safeScalar), GlobalSignals.BulletTimeVisualIntensity01);
            entry.SubsystemHeatPacked = PackFloatToMilliseconds(safeOverhead);
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private static uint PackBytesToMegabytes(long bytes)
        {
            if (bytes <= 0L)
                return 0u;

            long megabytes = bytes >> 20;
            return megabytes >= uint.MaxValue ? uint.MaxValue : (uint)megabytes;
        }

        private static uint PackTwoUnitFloats(float lhs01, float rhs01)
        {
            uint lhs = (uint)math.round(math.saturate(lhs01) * ushort.MaxValue);
            uint rhs = (uint)math.round(math.saturate(rhs01) * ushort.MaxValue);
            return lhs | (rhs << 16);
        }

        private static uint PackFloatToMilliseconds(float milliseconds)
        {
            if (!math.isfinite(milliseconds) || milliseconds <= 0f)
                return 0u;

            float scaled = milliseconds * 1000f;
            return scaled >= uint.MaxValue ? uint.MaxValue : (uint)scaled;
        }

        private static float SanitizeMilliseconds(float milliseconds)
        {
            return math.isfinite(milliseconds) && milliseconds > 0f
                ? milliseconds
                : 0f;
        }

        private static double LongestStepMillisecondsSafe()
        {
            return BootstrapStatus.LongestStepMilliseconds;
        }

        private static uint PackBootstrapPhaseDuration(BootstrapStepToken step, double elapsedMilliseconds)
        {
            double positiveMilliseconds = elapsedMilliseconds > 0d ? elapsedMilliseconds : 0d;
            uint wholeMilliseconds = positiveMilliseconds >= 16777215d
                ? 16777215u
                : (uint)(positiveMilliseconds + 0.5d);
            return ((uint)step << 24) | wholeMilliseconds;
        }

        private void InitializeBuffers()
        {
            if (_ringBuffer.IsCreated)
            {
                return;
            }

            if (!UnsafeUtility.IsBlittable<TelemetryEntry>() ||
                UnsafeUtility.SizeOf<CrashExportHeader>() != CrashExportHeaderSizeBytes ||
                UnsafeUtility.SizeOf<TelemetryEntry>() != TelemetryEntrySizeBytes)
            {
                enabled = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogError("CrashTelemetryBuffer requires fixed-size blittable crash export structs.");
#endif
                return;
            }

            // COLD ALLOC: NativeArray<TelemetryEntry>[1024] - lockless telemetry ring buffer - owner: CrashTelemetryBuffer
            _ringBuffer = new NativeArray<TelemetryEntry>(RingCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _ringBuffer,
                nameof(CrashTelemetryBuffer),
                nameof(_ringBuffer),
                NativeAllocationLifetime.Session);

            // COLD ALLOC: NativeArray<TelemetryEntry>[1000] - pre-crash binary export snapshot staging buffer - owner: CrashTelemetryBuffer
            _exportSnapshot = new NativeArray<TelemetryEntry>(ExportSnapshotEntries, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _exportSnapshot,
                nameof(CrashTelemetryBuffer),
                nameof(_exportSnapshot),
                NativeAllocationLifetime.Session);

            // COLD ALLOC: NativeArray<byte>[64016] - binary export scratch for 16B header + 1000 x 64B entries - owner: CrashTelemetryBuffer
            _exportScratch = new NativeArray<byte>(ExportScratchSizeBytes, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _exportScratch,
                nameof(CrashTelemetryBuffer),
                nameof(_exportScratch),
                NativeAllocationLifetime.Session);

            _liveTelemetryPath = HectonPersistentPathPolicy.CombineFile(LiveTelemetryFileName);
            _crashTelemetryPath = HectonPersistentPathPolicy.CombineFile(CrashTelemetryFileName);
            InitializeLiveTelemetryFile();
            InitializeCrashTelemetryFile();
            MemoryBudgetTracker.Register(
                MemoryBudgetOwnerName,
                ((long)_ringBuffer.Length * TelemetryEntrySizeBytes) +
                ((long)_exportSnapshot.Length * TelemetryEntrySizeBytes) +
                _exportScratch.Length,
                PersistentMemoryBudgetBytes);
            GlobalTelemetryBus.Initialize();
        }

        private void DisposeBuffers()
        {
            if (!StopBlackBoxExportThread())
                return;

            FlushQueuedCrashExportBeforeDispose();
            DisposeCrashTelemetryFile();
            DisposeLiveTelemetryFile();

            if (_ringBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_ringBuffer);
                _ringBuffer.Dispose();
            }

            if (_exportSnapshot.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_exportSnapshot);
                _exportSnapshot.Dispose();
            }

            if (_exportScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_exportScratch);
                _exportScratch.Dispose();
            }

            _ringBuffer = default;
            _exportSnapshot = default;
            _exportScratch = default;
            _liveTelemetryPath = null;
            _crashTelemetryPath = null;
            Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
            DisposeRecorder(ref _frameTimeRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
        }

        private bool StartBlackBoxExportThread()
        {
            lock (_blackBoxExportThreadGate)
            {
                if (_blackBoxExportThread != null)
                {
                    if (_blackBoxExportThread.IsAlive)
                        return true;

                    _blackBoxExportSignal?.Dispose();
                    _blackBoxExportSignal = null;
                    _blackBoxExportThread = null;
                }

                try
                {
                    Volatile.Write(ref _blackBoxExportStopRequested, 0);
                    // COLD ALLOC: AutoResetEvent[1] - persistent BLACKBOX export wake signal - owner: CrashTelemetryBuffer
                    _blackBoxExportSignal = new AutoResetEvent(false);
                    // COLD ALLOC: Thread[1] - dedicated BLACKBOX file export worker - owner: CrashTelemetryBuffer
                    _blackBoxExportThread = new Thread(RunBlackBoxExportThread)
                    {
                        IsBackground = true,
                        Name = BlackBoxExportThreadName,
                        Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.BackgroundIo)
                    };
                    _blackBoxExportThread.Start();
                    return true;
                }
                catch (Exception)
                {
                    _blackBoxExportSignal?.Dispose();
                    _blackBoxExportSignal = null;
                    _blackBoxExportThread = null;
                    RecordBlackBoxExportFailure();
                    return false;
                }
            }
        }

        private bool StopBlackBoxExportThread()
        {
            Thread exportThread;
            AutoResetEvent exportSignal;
            lock (_blackBoxExportThreadGate)
            {
                exportThread = _blackBoxExportThread;
                exportSignal = _blackBoxExportSignal;
                if (exportThread == null)
                {
                    exportSignal?.Dispose();
                    _blackBoxExportSignal = null;
                    Volatile.Write(ref _blackBoxExportStopRequested, 0);
                    return true;
                }

                Volatile.Write(ref _blackBoxExportStopRequested, 1);
                exportSignal?.Set();
            }

            if (!exportThread.Join(BlackBoxExportThreadJoinMilliseconds))
            {
                RecordBlackBoxExportFailure();
                return false;
            }

            lock (_blackBoxExportThreadGate)
            {
                if (ReferenceEquals(_blackBoxExportThread, exportThread))
                    _blackBoxExportThread = null;

                if (ReferenceEquals(_blackBoxExportSignal, exportSignal))
                    _blackBoxExportSignal = null;

                exportSignal?.Dispose();
                Volatile.Write(ref _blackBoxExportStopRequested, 0);
            }

            return true;
        }

        private void RunBlackBoxExportThread()
        {
            while (true)
            {
                AutoResetEvent exportSignal = Volatile.Read(ref _blackBoxExportSignal);
                if (exportSignal == null)
                    return;

                try
                {
                    exportSignal.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (Volatile.Read(ref _pendingExportSnapshotCount) > 0 ||
                    Volatile.Read(ref _pendingExportBytes) > 0)
                {
                    WritePreparedExportToDisk();
                }

                if (Volatile.Read(ref _blackBoxExportStopRequested) != 0)
                    return;
            }
        }

        private void FlushQueuedCrashExportBeforeDispose()
        {
            if (Volatile.Read(ref _exportState) == ExportStateIdle ||
                (Volatile.Read(ref _pendingExportSnapshotCount) <= 0 &&
                 Volatile.Read(ref _pendingExportBytes) <= 0))
            {
                return;
            }

            RecordBlackBoxExportDropped();
            ClearPendingExportState();
            Volatile.Write(ref _exportState, ExportStateIdle);
        }

        private void RecordBlackBoxExportFailure()
        {
            int current;
            int next;
            do
            {
                current = Volatile.Read(ref _pendingBlackBoxExportFailureCount);
                if (current >= BlackBoxExportFailureCounterMax)
                    break;

                next = current + 1;
            }
            while (Interlocked.CompareExchange(ref _pendingBlackBoxExportFailureCount, next, current) != current);

            OrRuntimeFaultFlags((int)ErrorBits.BlackBoxExportFault);
        }

        private void RecordBlackBoxExportDropped()
        {
            int current;
            int next;
            do
            {
                current = Volatile.Read(ref _pendingBlackBoxExportDroppedCount);
                if (current >= BlackBoxExportDroppedCounterMax)
                    break;

                next = current + 1;
            }
            while (Interlocked.CompareExchange(ref _pendingBlackBoxExportDroppedCount, next, current) != current);

            OrRuntimeFaultFlags((int)ErrorBits.BlackBoxExportDropped);
        }

        private void RecordBlackBoxExportSuppressed()
        {
            int current;
            int next;
            do
            {
                current = Volatile.Read(ref _pendingBlackBoxExportSuppressedCount);
                if (current >= BlackBoxExportSuppressedCounterMax)
                    break;

                next = current + 1;
            }
            while (Interlocked.CompareExchange(ref _pendingBlackBoxExportSuppressedCount, next, current) != current);

            OrRuntimeFaultFlags((int)ErrorBits.BlackBoxExportSuppressed);
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.logMessageReceived += HandleLogMessageReceived;
            Application.logMessageReceivedThreaded += HandleLogMessageReceivedThreaded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            Application.logMessageReceived -= HandleLogMessageReceived;
            Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
            _subscribed = false;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);

            if (!_registeredFixedTick)
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Core);
        }

        private void CacheRegistryPresenceCold()
        {
            Volatile.Write(ref _fluidRuntimePresent, GlobalRegistry.Fluid != null ? 1 : 0);
            Volatile.Write(ref _saveRuntimePresent, GlobalRegistry.SaveRuntime != null ? 1 : 0);
            Volatile.Write(ref _thermodynamicsRuntimePresent, GlobalRegistry.Thermodynamics != null ? 1 : 0);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.FluidRuntime:
                    Volatile.Write(ref _fluidRuntimePresent, currentService is HectonFluidEngine ? 1 : 0);
                    break;
                case GlobalRegistryServiceSlot.Save:
                    Volatile.Write(ref _saveRuntimePresent, currentService is SaveManager ? 1 : 0);
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsRuntime:
                    Volatile.Write(ref _thermodynamicsRuntimePresent, currentService is AbyssalThermalManager ? 1 : 0);
                    break;
            }
        }

        private bool TryRegisterRuntimeService()
        {
            if (_runtimeRegistered || !Application.isPlaying)
                return true;

            CrashTelemetryBuffer registeredInstance = GlobalRegistry.CrashTelemetry;
            if (registeredInstance != null && registeredInstance != this)
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterCrashTelemetryRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.CrashTelemetry, this);
            return _runtimeRegistered;
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Core);
                _registeredFixedTick = false;
            }
        }

        private void TryUnregisterRuntimeService()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterCrashTelemetryRuntime(this);
            _runtimeRegistered = false;
        }

        private void ResolvePlayerTransform(float dt)
        {
            if (_playerTransform != null)
                return;

            _playerResolveCooldown -= dt;
            if (_playerResolveCooldown > 0f)
                return;

            _playerResolveCooldown = PlayerResolveCooldownSeconds;
            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                _playerTransform = playerTransform;
                if (_survivalSystem == null && _playerTransform != null)
                    _playerTransform.TryGetComponent(out _survivalSystem);

                _playerRigidbody = null;
                _playerMovement = null;
                if (_playerTransform != null)
                {
                    _playerTransform.TryGetComponent(out _playerRigidbody);
                    _playerTransform.TryGetComponent(out _playerMovement);
                }
            }
        }

        private uint SampleSystemMask()
        {
            uint systemMask = 0u;
            if (Volatile.Read(ref _fluidRuntimePresent) != 0)
            {
                systemMask |= (uint)SystemBits.Physics;
                systemMask |= (uint)SystemBits.Fluid;
            }

            if (HectonVoxelEngine.ActiveRuntimeInstance != null)
                systemMask |= (uint)SystemBits.Voxel;

            if (HectonDirectorAI.ActiveRuntimeInstance != null)
                systemMask |= (uint)SystemBits.AI;

            if (Volatile.Read(ref _saveRuntimePresent) != 0)
                systemMask |= (uint)SystemBits.Save;

            return systemMask;
        }

        private static uint SampleActiveChunkCount()
        {
            HectonVoxelEngine voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;
            return voxelEngine != null
                ? unchecked((uint)math.max(0, voxelEngine.ActiveVolumeCount))
                : 0u;
        }

        private void ResolveProfilerRecorders()
        {
            _availableProfilerHandles.Clear();
            ProfilerRecorderHandle.GetAvailable(_availableProfilerHandles);
            _frameTimeRecorder = StartRecorderFromAvailable(_FrameTimeCandidates);
            _gcAllocRecorder = StartRecorderFromAvailable(_GcAllocCandidates);
            _availableProfilerHandles.Clear();
        }

        private uint SamplePlayerVelocityPacked()
        {
            if (_playerRigidbody == null)
                return 0u;

            Vector3 velocity = _playerRigidbody.linearVelocity;
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            if (!math.all(math.isfinite(velocity3)))
                return 0u;

            return PackSignedVectorComponent(velocity3.x) |
                   (PackSignedVectorComponent(velocity3.y) << 10) |
                   (PackSignedVectorComponent(velocity3.z) << 20);
        }

        private float3 SamplePlayerPosition(out bool hasPlayer)
        {
            if (_playerTransform == null)
            {
                hasPlayer = false;
                return float3.zero;
            }

            hasPlayer = true;
            if (_playerMovement != null)
            {
                double3 absolute = _playerMovement.CurrentAup.ToAbsoluteDouble3();
                float3 playerAup = new float3((float)absolute.x, (float)absolute.y, (float)absolute.z);
                if (math.all(math.isfinite(playerAup)))
                    return playerAup;
            }

            Vector3 runtimePosition = _playerTransform.position;
            return ToAbsoluteUniversePosition(runtimePosition);
        }

        private static float3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            double3 runtimePositionDouble = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            double3 bridgeUniversePosition = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(runtimePosition);
            double3 absolutePosition = math.lengthsq(bridgeUniversePosition - runtimePositionDouble) > 0.000000001d
                ? bridgeUniversePosition
                : TryResolveRuntimeAupDouble(runtimePosition, out double3 resolvedAup)
                    ? resolvedAup
                    : double3.zero;
            return new float3((float)absolutePosition.x, (float)absolutePosition.y, (float)absolutePosition.z);
        }

        private static bool TryResolveRuntimeAupDouble(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z))))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            absolutePosition = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private uint BuildErrorFlags(float dt, float reservedMemoryMb, float3 playerPos, bool hasPlayer)
        {
            uint errorFlags = 0u;

            if (!hasPlayer)
                errorFlags |= (uint)ErrorBits.MissingPlayer;

            if (!math.isfinite(dt) || dt < 0f)
                errorFlags |= (uint)ErrorBits.NonFiniteDeltaTime;

            if (!math.all(math.isfinite(playerPos)))
                errorFlags |= (uint)ErrorBits.NonFinitePlayerPosition;

            if (dt >= SevereFrameTimeSeconds)
                errorFlags |= (uint)ErrorBits.FrameBudgetExceeded;

            if (dt > CriticalFrameTimeSeconds)
                errorFlags |= (uint)ErrorBits.CriticalPerformanceSpike;

            if (reservedMemoryMb >= MaximumReservedMemoryMb)
                errorFlags |= (uint)ErrorBits.ReservedMemoryExceeded;

            if (math.lengthsq(playerPos) > (MaximumTrackedWorldMagnitude * MaximumTrackedWorldMagnitude))
                errorFlags |= (uint)ErrorBits.OutOfBoundsPlayerPosition;

            errorFlags |= BootstrapStatus.GetTelemetryErrorFlags();

            return errorFlags;
        }

        private float SampleGpuFrameTimeMs()
        {
            uint sampleCount = FrameTimingManager.GetLatestTimings(1u, _frameTimingScratch);
            if (sampleCount == 0u)
                return 0f;

            return (float)_frameTimingScratch[0].gpuFrameTime;
        }

        private void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            uint faultFlags = 0u;
            ExportReason exportReason = ExportReason.ErrorFlags;

            if (type == LogType.Exception)
            {
                faultFlags = (uint)ErrorBits.ExceptionLogged;
                exportReason = ExportReason.UnityException;
            }
            else if (type == LogType.Error || type == LogType.Assert)
            {
                faultFlags = (uint)ErrorBits.ErrorLogged;
                exportReason = ExportReason.UnityError;
            }

            if (faultFlags == 0u)
                return;

            OrStickyErrorFlags(faultFlags);
            WriteLogFaultTelemetry(
                GlobalTelemetryBus.ComputeContextHash(condition),
                GlobalTelemetryBus.ComputeContextHash(stackTrace),
                faultFlags,
                exportReason);
            Interlocked.Exchange(ref _threadedFaultFlags, 0);
            TryExportSnapshot(exportReason, faultFlags, bypassCooldown: false);
        }

        private void HandleLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            uint faultFlags = 0u;
            if (type == LogType.Exception)
            {
                faultFlags = (uint)ErrorBits.ExceptionLogged;
            }
            else if (type == LogType.Error || type == LogType.Assert)
            {
                faultFlags = (uint)ErrorBits.ErrorLogged;
            }

            if (faultFlags == 0u)
                return;

            OrThreadedFaultFlags(unchecked((int)faultFlags));
            GlobalTelemetryBus.PublishUnityLogFault(
                GlobalTelemetryBus.ComputeContextHash(condition),
                GlobalTelemetryBus.ComputeContextHash(stackTrace),
                faultFlags);

            if (type == LogType.Exception)
                GlobalTelemetryBus.RequestEmergencyFlushAsync();
        }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
        {
            uint exportFlags = (uint)ErrorBits.ExceptionLogged;
            OrStickyErrorFlags(exportFlags);
            OrThreadedFaultFlags(unchecked((int)exportFlags));
            TryExportSnapshotFromUnhandledException(exportFlags);
        }

        private void OrStickyErrorFlags(uint flags)
        {
            int intFlags = unchecked((int)flags);
            int snapshot;
            int combined;
            do
            {
                snapshot = Volatile.Read(ref _stickyErrorFlags);
                combined = snapshot | intFlags;
            }
            while (Interlocked.CompareExchange(ref _stickyErrorFlags, combined, snapshot) != snapshot);
        }

        private void WriteLogFaultTelemetry(
            uint conditionHash,
            uint stackHash,
            uint faultFlags,
            ExportReason exportReason)
        {
            if (!_ringBuffer.IsCreated)
                return;

            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = ReserveTelemetryWriteIndex();
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            TelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            entry.LatencyMs = _lastLatencyMs;
            entry.GpuFrameTime = SampleGpuFrameTimeMs();
            entry.MemoryUsedMb = SampleReservedMemoryMegabytes();
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = faultFlags;
            entry.ExportReason = (uint)exportReason;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = conditionHash;
            entry.SubsystemHeatPacked = stackHash;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
        }

        private void OrThreadedFaultFlags(int flags)
        {
            int snapshot;
            int combined;
            do
            {
                snapshot = Volatile.Read(ref _threadedFaultFlags);
                combined = snapshot | flags;
            }
            while (Interlocked.CompareExchange(ref _threadedFaultFlags, combined, snapshot) != snapshot);
        }

        private static void OrRuntimeFaultFlags(int flags)
        {
            int snapshot;
            int combined;
            do
            {
                snapshot = Volatile.Read(ref _runtimeFaultFlags);
                combined = snapshot | flags;
            }
            while (Interlocked.CompareExchange(ref _runtimeFaultFlags, combined, snapshot) != snapshot);
        }

        private static uint PackAiState()
        {
            HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;
            if (director == null)
                return 0u;

            uint phase = unchecked((uint)math.max(0, director.CurrentPhaseIndex)) & 0xFFu;
            uint stress = QuantizeUnitToByte(director.CurrentStress01);
            uint intensity = QuantizeUnitToByte(director.CurrentIntensity01);
            uint predatorPressure = director.IsPredatorPressureEnabled ? 1u : 0u;
            return phase |
                   (stress << 8) |
                   (intensity << 16) |
                   (predatorPressure << 24);
        }

        private uint PackSubsystemHeat()
        {
            float heatSeverity = _survivalSystem != null ? _survivalSystem.HeatStressSeverity01 : 0f;
            float environmentTemperature = _survivalSystem != null ? _survivalSystem.EnvironmentTemperature : 0f;
            float internalTemperature = _survivalSystem != null ? _survivalSystem.InternalTemperature : 0f;

            uint heat = QuantizeUnitToByte(heatSeverity);
            uint environment = QuantizeSignedTemperatureToByte(environmentTemperature);
            uint internalValue = QuantizeSignedTemperatureToByte(internalTemperature);
            uint thermalRuntimePresent = Volatile.Read(ref _thermodynamicsRuntimePresent) != 0 ? 1u : 0u;
            return heat |
                   (environment << 8) |
                   (internalValue << 16) |
                   (thermalRuntimePresent << 24);
        }

        private ProfilerRecorder StartRecorderFromAvailable(string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return default;

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                string candidate = candidates[candidateIndex];
                for (int handleIndex = 0; handleIndex < _availableProfilerHandles.Count; handleIndex++)
                {
                    ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(_availableProfilerHandles[handleIndex]);
                    if (!MatchesCandidate(description.Name, candidate))
                        continue;

                    try
                    {
                        ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                            description.Category,
                            description.Name,
                            RecorderCapacity,
                            ProfilerRecorderOptions.Default);
                        if (recorder.Valid)
                            return recorder;
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return default;
        }

        private void InitializeLiveTelemetryFile()
        {
            DisposeLiveTelemetryFile();
            if (string.IsNullOrEmpty(_liveTelemetryPath))
                return;

            lock (_liveTelemetryFileGate)
            {
                try
                {
                    string directory = Path.GetDirectoryName(_liveTelemetryPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    _liveTelemetryStream = new FileStream(_liveTelemetryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    EnsureTelemetryBackingFileSize(_liveTelemetryStream, _liveTelemetryPath, LiveTelemetryRecordSizeBytes);

                    Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
                }
                catch (Exception exception)
                {
                    DisposeLiveTelemetryFile();
                    _liveTelemetryPath = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    H8Debug.LogException(exception);
#endif
                }
            }
        }

        private void DisposeLiveTelemetryFile()
        {
            lock (_liveTelemetryFileGate)
            {
                Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
                _liveTelemetryStream?.Dispose();
                _liveTelemetryStream = null;
            }
        }

        private void InitializeCrashTelemetryFile()
        {
            DisposeCrashTelemetryFile();
            if (string.IsNullOrEmpty(_crashTelemetryPath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(_crashTelemetryPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                _crashTelemetryStream = new FileStream(_crashTelemetryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                EnsureTelemetryBackingFileSize(_crashTelemetryStream, _crashTelemetryPath, ExportScratchSizeBytes);
            }
            catch (Exception exception)
            {
                DisposeCrashTelemetryFile();
                _crashTelemetryPath = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
        }

        private void DisposeCrashTelemetryFile()
        {
            if (!StopBlackBoxExportThread())
                return;

            FlushQueuedCrashExportBeforeDispose();
            lock (_crashTelemetryFileGate)
            {
                ClearPendingExportState();
                Volatile.Write(ref _exportState, ExportStateIdle);
                _crashTelemetryStream?.Dispose();
                _crashTelemetryStream = null;
            }
        }

        private static void EnsureTelemetryBackingFileSize(FileStream stream, string path, long expectedBytes)
        {
            if (!TryGetTelemetryBackingFileLength(path, out long currentBytes) || currentBytes != expectedBytes)
                stream.SetLength(expectedBytes);
        }

        private static bool TryGetTelemetryBackingFileLength(string path, out long fileLength)
        {
            fileLength = 0L;
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists)
                    return false;

                fileLength = info.Length;
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void TryWriteLiveTelemetry(uint frameIndex, float dt, float reservedMemoryMb, uint activeChunkCount)
        {
            if (string.IsNullOrEmpty(_liveTelemetryPath))
                return;

            int frameNumber = unchecked((int)frameIndex);
            if (frameNumber <= 0 || frameNumber - _lastLiveTelemetryWriteFrame < LiveTelemetryWriteIntervalFrames)
                return;

            LiveTelemetryRecord record = default;
            record.Magic = LiveTelemetryMagic;
            record.Version = LiveTelemetryVersion;
            record.FrameIndex = frameIndex;
            record.ActiveChunkCount = activeChunkCount;
            record.GcAllocBytes = unchecked((uint)math.max(0, ReadIntValue(_gcAllocRecorder)));
            record.CpuFrameTimeMs = ReadMilliseconds(_frameTimeRecorder);
            record.DeltaTime = dt;
            record.ReservedMemoryMb = reservedMemoryMb;

            if (Interlocked.CompareExchange(ref _liveTelemetryWriteState, LiveTelemetryStateQueued, LiveTelemetryStateIdle) != LiveTelemetryStateIdle)
                return;

            _pendingLiveTelemetryRecord = record;
            bool queued = false;
            try
            {
                queued = ThreadPool.UnsafeQueueUserWorkItem(_backgroundLiveTelemetryCallback, this);
                if (queued)
                    _lastLiveTelemetryWriteFrame = frameNumber;
            }
            catch (Exception)
            {
            }
            finally
            {
                if (!queued)
                    Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
            }
        }

        private static void ExecuteBackgroundLiveTelemetryWrite(object state)
        {
            if (state is CrashTelemetryBuffer crashTelemetryBuffer)
                crashTelemetryBuffer.WritePendingLiveTelemetryToFile();
        }

        private void WritePendingLiveTelemetryToFile()
        {
            try
            {
                lock (_liveTelemetryFileGate)
                {
                    if (_liveTelemetryStream == null)
                        return;

                    LiveTelemetryRecord record = _pendingLiveTelemetryRecord;
                    unsafe
                    {
                        fixed (byte* scratch = _liveTelemetryFileScratch)
                        {
                            UnsafeUtility.MemClear(scratch, LiveTelemetryRecordSizeBytes);
                            UnsafeUtility.CopyStructureToPtr(ref record, scratch);
                        }
                    }

                    _liveTelemetryStream.Position = 0L;
                    _liveTelemetryStream.Write(_liveTelemetryFileScratch, 0, LiveTelemetryRecordSizeBytes);
                    _liveTelemetryStream.SetLength(LiveTelemetryRecordSizeBytes);
                    _liveTelemetryStream.Flush(true);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
            catch (IOException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
            catch (Exception)
            {
            }
            finally
            {
                Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
            }
        }

        private void TryExportSnapshot(ExportReason exportReason, uint exportFlags, bool bypassCooldown)
        {
            if (!_ringBuffer.IsCreated)
                return;

            if (Interlocked.CompareExchange(ref _exportState, ExportStateQueued, ExportStateIdle) != ExportStateIdle)
            {
                if ((exportFlags & ((uint)ErrorBits.NanPhysics | (uint)ErrorBits.PhysiologyNan)) != 0u)
                    OrRuntimeFaultFlags(unchecked((int)exportFlags));

                RecordBlackBoxExportDropped();
                return;
            }

            bool exportQueued = false;
            try
            {
                int currentFrame = Time.frameCount;
                bool shouldBypassCooldown = bypassCooldown || (exportFlags & ExportCooldownBypassMask) != 0u;
                if (!shouldBypassCooldown && currentFrame - _lastExportFrame < ExportCooldownFrames)
                {
                    RecordBlackBoxExportSuppressed();
                    return;
                }

                using (ProfilerRegistry.TelemetryExport.Auto())
                {
                    int snapshotCount = SnapshotRecentEntries(exportReason);
                    if (snapshotCount <= 0)
                        return;

                    Volatile.Write(ref _pendingExportSnapshotCount, snapshotCount);
                    Volatile.Write(ref _pendingExportFrame, currentFrame);

                    Volatile.Write(ref _pendingExportBytes, 0);
                    exportQueued = QueueBackgroundExport();
                    if (!exportQueued)
                        ClearPendingExportState();
                }
            }
            finally
            {
                if (!exportQueued)
                    Volatile.Write(ref _exportState, ExportStateIdle);
            }
        }

        private void TryExportSnapshotFromUnhandledException(uint exportFlags)
        {
            if (!_ringBuffer.IsCreated)
                return;

            if (Interlocked.CompareExchange(ref _exportState, ExportStateQueued, ExportStateIdle) != ExportStateIdle)
            {
                RecordBlackBoxExportDropped();
                return;
            }

            bool exportQueued = false;
            try
            {
                int snapshotCount = SnapshotRecentEntries(ExportReason.AppDomainUnhandledException);
                if (snapshotCount <= 0)
                    return;

                Volatile.Write(ref _pendingExportSnapshotCount, snapshotCount);
                Volatile.Write(ref _pendingExportFrame, GetCrashSafeExportFrame());
                Volatile.Write(ref _pendingExportBytes, 0);
                GlobalTelemetryBus.RequestEmergencyFlushAsync();
                exportQueued = QueueBackgroundExport();
            }
            catch (Exception)
            {
            }
            finally
            {
                if (!exportQueued)
                {
                    ClearPendingExportState();
                    Volatile.Write(ref _exportState, ExportStateIdle);
                }
            }
        }

        private int SnapshotRecentEntries(ExportReason exportReason)
        {
            if (!_ringBuffer.IsCreated || !_exportSnapshot.IsCreated)
                return 0;

            long writeCursor = Volatile.Read(ref _writeCursor);
            long committedEntries = math.min(writeCursor, RingCapacity);
            long skipNewestEntry = exportReason == ExportReason.ErrorFlags ? 1L : 0L;
            long availableEntries = math.min(ExportSnapshotEntries, committedEntries - skipNewestEntry);
            if (availableEntries <= 0)
                return 0;

            int entryCount = (int)availableEntries;
            long startCursor = writeCursor - skipNewestEntry - availableEntries;
            int sourceStart = (int)startCursor & RingCapacityMask;

            unsafe
            {
                byte* sourceBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_ringBuffer);
                byte* destinationBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_exportSnapshot);
                int firstCopyCount = math.min(entryCount, RingCapacity - sourceStart);
                int firstCopyBytes = firstCopyCount * TelemetryEntrySizeBytes;
                int totalCopyBytes = entryCount * TelemetryEntrySizeBytes;
                int destinationBytes = _exportSnapshot.Length * TelemetryEntrySizeBytes;

                if (!UnsafeMemoryCopyGuard.TryMemCpy(
                        destinationBase,
                        destinationBytes,
                        sourceBase + (sourceStart * TelemetryEntrySizeBytes),
                        firstCopyBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(CrashTelemetryBuffer));
                    return 0;
                }

                int remainingBytes = totalCopyBytes - firstCopyBytes;
                if (remainingBytes > 0 &&
                    !UnsafeMemoryCopyGuard.TryMemCpy(
                        destinationBase + firstCopyBytes,
                        destinationBytes - firstCopyBytes,
                        sourceBase,
                        remainingBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(CrashTelemetryBuffer));
                    return 0;
                }
            }

            return entryCount;
        }

        private int BuildExportScratch(int snapshotCount)
        {
            if (snapshotCount <= 0 || !_exportScratch.IsCreated || !_exportSnapshot.IsCreated)
                return 0;

            unsafe
            {
                CrashExportHeader header = default;
                header.Magic = BinaryMagic;
                header.EntryCount = unchecked((uint)snapshotCount);
                header.StructSizeBytes = TelemetryEntrySizeBytes;

                int entryBytes = snapshotCount * TelemetryEntrySizeBytes;
                int totalBytes = CrashExportHeaderSizeBytes + entryBytes;

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_exportScratch);
                UnsafeUtility.CopyStructureToPtr(ref header, destination);
                void* snapshotPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_exportSnapshot);
                int destinationBytes = _exportScratch.Length - CrashExportHeaderSizeBytes;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(
                        destination + CrashExportHeaderSizeBytes,
                        destinationBytes,
                        snapshotPtr,
                        entryBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(CrashTelemetryBuffer));
                    return 0;
                }

                return totalBytes;
            }
        }

        private bool QueueBackgroundExport()
        {
            try
            {
                if (!StartBlackBoxExportThread())
                    return false;

                AutoResetEvent exportSignal = Volatile.Read(ref _blackBoxExportSignal);
                if (exportSignal == null)
                {
                    RecordBlackBoxExportFailure();
                    return false;
                }

                exportSignal.Set();
                return true;
            }
            catch (ObjectDisposedException)
            {
                RecordBlackBoxExportFailure();
                return false;
            }
            catch (Exception)
            {
                RecordBlackBoxExportFailure();
                return false;
            }
        }

        private bool WritePreparedExportToDisk()
        {
            bool wroteExport = false;
            bool hadPendingExport =
                Volatile.Read(ref _pendingExportBytes) > 0 ||
                Volatile.Read(ref _pendingExportSnapshotCount) > 0;

            try
            {
                lock (_crashTelemetryFileGate)
                {
                    if (!_exportScratch.IsCreated)
                        return false;

                    int exportBytes = Volatile.Read(ref _pendingExportBytes);
                    if (exportBytes <= 0)
                    {
                        exportBytes = BuildExportScratch(Volatile.Read(ref _pendingExportSnapshotCount));
                        Volatile.Write(ref _pendingExportBytes, exportBytes);
                    }

                    if (_crashTelemetryStream == null || exportBytes <= 0)
                        return false;

                    unsafe
                    {
                        fixed (byte* destination = _crashExportFileScratch)
                        {
                            UnsafeUtility.MemClear(destination, ExportScratchSizeBytes);
                            void* exportPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_exportScratch);
                            if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, ExportScratchSizeBytes, exportPtr, exportBytes))
                            {
                                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(CrashTelemetryBuffer));
                                return false;
                            }
                        }
                    }

                    _crashTelemetryStream.Position = 0L;
                    _crashTelemetryStream.Write(_crashExportFileScratch, 0, ExportScratchSizeBytes);
                    _crashTelemetryStream.SetLength(ExportScratchSizeBytes);
                    _crashTelemetryStream.Flush(true);
                    wroteExport = true;
                    int exportFrame = Volatile.Read(ref _pendingExportFrame);
                    if (exportFrame >= 0)
                        Volatile.Write(ref _lastExportFrame, exportFrame);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
            catch (IOException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
            catch (Exception)
            {
            }
            finally
            {
                if (!wroteExport && hadPendingExport)
                    RecordBlackBoxExportFailure();

                ClearPendingExportState();
                Volatile.Write(ref _exportState, ExportStateIdle);
            }

            return wroteExport;
        }

        private int GetCrashSafeExportFrame()
        {
            long cursor = Volatile.Read(ref _writeCursor);
            if (cursor <= 0L)
                return 0;

            return cursor >= int.MaxValue ? int.MaxValue : (int)cursor;
        }

        private void ClearPendingExportState()
        {
            Volatile.Write(ref _pendingExportSnapshotCount, 0);
            Volatile.Write(ref _pendingExportBytes, 0);
            Volatile.Write(ref _pendingExportFrame, int.MinValue);
        }

        private static float ReadMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0f;

            if (recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds)
                return recorder.LastValue * NanosecondsToMilliseconds;

            return (float)recorder.LastValue;
        }

        private static int ReadIntValue(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0;

            long value = recorder.LastValue;
            if (value <= 0L)
                return 0;

            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();

            recorder = default;
        }

        private static void PublishPerformanceWarningNoThrow(uint warningHash, uint contextHash, float scalarValue)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, scalarValue);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
        }

        private static bool MatchesCandidate(string value, string candidate)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(candidate))
                return false;

            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase) ||
                   value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static uint QuantizeUnitToByte(float value)
        {
            return unchecked((uint)math.clamp(math.round(math.saturate(value) * 255f), 0f, 255f));
        }

        private static uint QuantizeSignedTemperatureToByte(float temperatureCelsius)
        {
            float normalized = math.saturate((temperatureCelsius + 50f) * SignedTemperatureToUnit);
            return QuantizeUnitToByte(normalized);
        }

        private static uint PackSignedVectorComponent(float value)
        {
            float clamped = math.clamp(value, -511f, 511f);
            int quantized = (int)math.round(clamped);
            return unchecked((uint)(quantized + 511) & 0x3FFu);
        }

    }
}
