using System;
using System.IO;
using System.IO.MemoryMappedFiles;
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
    public sealed class CrashTelemetryBuffer : MonoBehaviour, ITickable, IUpdatable, IFixedTickable
    {
        private const int RingCapacity = 300;
        private const int ExportSnapshotEntries = RingCapacity;
        private const int ExportCooldownFrames = 30;
        private const int DebugLogEntrySizeBytes = 64;
        private const int CrashExportHeaderSizeBytes = 16;
        private const int ExportScratchSizeBytes = CrashExportHeaderSizeBytes + (ExportSnapshotEntries * DebugLogEntrySizeBytes);
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
        private const float MaximumTrackedWorldMagnitude = 1000000f;
        private const float MaximumReservedMemoryMb = 4096f;
        private const uint LiveTelemetryMagic = 0x4D4C4554u; // "TELM"
        private const uint LiveTelemetryVersion = 1u;
        private const ulong BinaryMagic = 0x00384E4F54434548ul; // "HECTON8\0" in little-endian byte order.
        private const string ExportFilePrefix = "crash_";
        private const string ExportFileExtension = ".hbin";
        private const string ExportTimestampFormat = "yyyyMMdd_HHmmss_fff";
        private const string LiveTelemetryFileName = "runtime_telemetry.bin";
        private const string CrashTelemetryFileName = "crash_telemetry_latest.hbin";
        private const long PersistentMemoryBudgetBytes = 786432L;
        private const string MemoryBudgetOwnerName = "CrashTelemetryBuffer";
        private static readonly string[] _FrameTimeCandidates =
        {
            "CPU Total Frame Time",
            "Frame Time"
        };

        private static readonly string[] _GcAllocCandidates = { "GC Allocated In Frame" };

        private static readonly WaitCallback _backgroundExportCallback = ExecuteBackgroundExport;
        private static readonly WaitCallback _backgroundLiveTelemetryCallback = ExecuteBackgroundLiveTelemetryWrite;
        private static CrashTelemetryBuffer _instance;
        private static int _runtimeFaultFlags;

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
        }

        [StructLayout(LayoutKind.Sequential, Size = CrashExportHeaderSizeBytes)]
        private struct CrashExportHeader
        {
            public ulong Magic;
            public uint EntryCount;
            public uint StructSizeBytes;
        }

        [StructLayout(LayoutKind.Sequential, Size = DebugLogEntrySizeBytes)]
        private struct DebugLogEntry
        {
            public uint FrameIndex;
            public uint SystemMask;
            public float DeltaTime;
            public float FixedDeltaTime;
            public float GpuFrameTime;
            public float MemoryUsedMb;
            public float3 PlayerAup;
            public uint ActiveChunkCount;
            public uint ErrorFlags;
            public uint ExportReason;
            public uint AupShiftSequence;
            public uint AiStatePacked;
            public uint SubsystemHeatPacked;
            public uint LastOriginShiftFrame;
        }

        [StructLayout(LayoutKind.Sequential, Size = LiveTelemetryRecordSizeBytes)]
        private struct LiveTelemetryRecord
        {
            public uint Magic;
            public uint Version;
            public uint FrameIndex;
            public uint ActiveChunkCount;
            public uint GcAllocBytes;
            public float CpuFrameTimeMs;
            public float DeltaTime;
            public float ReservedMemoryMb;
        }

        private NativeArray<DebugLogEntry> _ringBuffer;
        private NativeArray<DebugLogEntry> _exportSnapshot;
        private Transform _playerTransform;
        private HectonSurvivalSystem _survivalSystem;
        private float _playerResolveCooldown;
        private float _nextOriginShiftTelemetryTime;
        private float _lastFixedDeltaTime;
        private long _writeCursor;
        private uint _stickyErrorFlags;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private int _lastExportFrame = int.MinValue;
        private int _threadedFaultFlags;
        private int _exportState;
        private int _liveTelemetryWriteState;
        private int _pendingExportBytes;
        private string _pendingExportPath;
        private string _liveTelemetryPath;
        private string _crashTelemetryPath;
        private FileStream _liveTelemetryStream;
        private MemoryMappedFile _liveTelemetryMmf;
        private MemoryMappedViewAccessor _liveTelemetryView;
        private FileStream _crashTelemetryStream;
        private MemoryMappedFile _crashTelemetryMmf;
        private MemoryMappedViewAccessor _crashTelemetryView;
        private NativeArray<byte> _exportScratch;
        private LiveTelemetryRecord _pendingLiveTelemetryRecord;
        private ProfilerRecorder _frameTimeRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private int _lastLiveTelemetryWriteFrame = int.MinValue;

        // COLD ALLOC: FrameTiming[1] - reusable GPU timing sample buffer - owner: CrashTelemetryBuffer
        private readonly FrameTiming[] _frameTimingScratch = new FrameTiming[1];
        // COLD ALLOC: List<ProfilerRecorderHandle>[64] - profiler recorder resolution scratch - owner: CrashTelemetryBuffer
        private readonly List<ProfilerRecorderHandle> _availableProfilerHandles = new List<ProfilerRecorderHandle>(64);

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only view model for the latest retained crash telemetry frames.
        /// </summary>
        public readonly struct EditorSnapshotEntry
        {
            public readonly uint FrameIndex;
            public readonly uint SystemMask;
            public readonly float DeltaTime;
            public readonly float FixedDeltaTime;
            public readonly float GpuFrameTime;
            public readonly float MemoryUsedMb;
            public readonly Vector3 PlayerAup;
            public readonly uint ActiveChunkCount;
            public readonly uint ErrorFlags;
            public readonly uint ExportReason;

            public EditorSnapshotEntry(
                uint frameIndex,
                uint systemMask,
                float deltaTime,
                float fixedDeltaTime,
                float gpuFrameTime,
                float memoryUsedMb,
                Vector3 playerAup,
                uint activeChunkCount,
                uint errorFlags,
                uint exportReason)
            {
                FrameIndex = frameIndex;
                SystemMask = systemMask;
                DeltaTime = deltaTime;
                FixedDeltaTime = fixedDeltaTime;
                GpuFrameTime = gpuFrameTime;
                MemoryUsedMb = memoryUsedMb;
                PlayerAup = playerAup;
                ActiveChunkCount = activeChunkCount;
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
            if (_instance != null)
                return _instance;

            GameObject telemetryObject = new GameObject("[CrashTelemetryBuffer]");
            return telemetryObject.AddComponent<CrashTelemetryBuffer>();
        }

        /// <summary>
        /// Reports a physics NaN recovery into the telemetry error stream.
        /// </summary>
        public static void ReportNanPhysicsRecovery()
        {
            OrRuntimeFaultFlags((int)ErrorBits.NanPhysics);
        }

        /// <summary>
        /// Reports a critical save-system fault into the crash telemetry stream.
        /// </summary>
        public static void ReportSaveSystemCriticalFault()
        {
            OrRuntimeFaultFlags((int)ErrorBits.ErrorLogged);
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
        /// Reports a NativeQueue event bus that exceeded its frame budget for consecutive frames.
        /// </summary>
        /// <param name="queueHash">Stable queue identifier hash.</param>
        /// <param name="pendingCount">Pending payload count at the congestion point.</param>
        /// <param name="entityCount">Current spatial entity count.</param>
        public static void ReportBusCongestionWarning(uint queueHash, int pendingCount, int entityCount)
        {
            OrRuntimeFaultFlags((int)ErrorBits.BusCongestionWarning);
            CrashTelemetryBuffer instance = _instance;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteBusCongestionTelemetry(queueHash, pendingCount, entityCount);
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
            CrashTelemetryBuffer instance = _instance;
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
            CrashTelemetryBuffer instance = _instance;
            if (instance == null || !instance._ringBuffer.IsCreated)
                return;

            instance.WriteKineticAnomalyTelemetry(runtimePosition, deltaVelocity, accelerationMetersPerSecondSq);
        }

        /// <summary>
        /// Writes one bootstrap phase duration sample into the crash telemetry ring.
        /// </summary>
        /// <param name="step">Bootstrap phase token.</param>
        /// <param name="elapsedMilliseconds">Measured phase duration in milliseconds.</param>
        public static void RecordBootstrapPhaseDuration(BootstrapStepToken step, double elapsedMilliseconds)
        {
            CrashTelemetryBuffer instance = _instance;
            if (instance == null || !instance._ringBuffer.IsCreated || step == BootstrapStepToken.None)
                return;

            instance.WriteBootstrapPhaseDuration(step, elapsedMilliseconds);
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

            long committedEntries = math.min(_writeCursor, ExportSnapshotEntries);
            if (committedEntries <= 0)
                return 0;

            long startCursor = _writeCursor - committedEntries;
            for (int i = 0; i < committedEntries; i++)
            {
                int ringIndex = (int)((startCursor + i) % RingCapacity);
                DebugLogEntry entry = _ringBuffer[ringIndex];
                destination.Add(new EditorSnapshotEntry(
                    entry.FrameIndex,
                    entry.SystemMask,
                    entry.DeltaTime,
                    entry.FixedDeltaTime,
                    entry.GpuFrameTime,
                    entry.MemoryUsedMb,
                    new Vector3(entry.PlayerAup.x, entry.PlayerAup.y, entry.PlayerAup.z),
                    entry.ActiveChunkCount,
                    entry.ErrorFlags,
                    entry.ExportReason));
            }

            return destination.Count;
        }
#endif

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            InitializeBuffers();
            ResolveProfilerRecorders();

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnEnable()
        {
            Subscribe();
            TryRegister();
        }

        private void OnDisable()
        {
            Unsubscribe();
            TryUnregister();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            TryUnregister();
            DisposeBuffers();

            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            if (_stickyErrorFlags != 0u)
                TryExportSnapshot(ExportReason.ApplicationQuit, _stickyErrorFlags, writeSynchronously: true);

            DisposeBuffers();
        }

        /// <summary>
        /// Records one telemetry entry on the shared game tick.
        /// </summary>
        /// <param name="dt">Frame delta passed by <see cref="GameTickManager"/>.</param>
        public void Tick(float dt)
        {
            if (!_ringBuffer.IsCreated)
                return;

            using (ProfilerRegistry.TelemetryWrite.Auto())
            {
                ResolvePlayerTransform(dt);

                FrameTimingManager.CaptureFrameTimings();
                float gpuFrameTime = SampleGpuFrameTimeMs();
                float reservedMemoryMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
                float3 playerAup = SamplePlayerPosition(out bool hasPlayer);
                uint systemMask = SampleSystemMask();
                uint activeChunkCount = SampleActiveChunkCount();
                uint errorFlags = BuildErrorFlags(dt, reservedMemoryMb, playerAup, hasPlayer);
                OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
                uint threadedFaultFlags = unchecked((uint)Interlocked.Exchange(ref _threadedFaultFlags, 0));
                uint runtimeFaultFlags = unchecked((uint)Interlocked.Exchange(ref _runtimeFaultFlags, 0));
                if (threadedFaultFlags != 0u)
                    errorFlags |= threadedFaultFlags;
                if (runtimeFaultFlags != 0u)
                    errorFlags |= runtimeFaultFlags;

                uint frameIndex = unchecked((uint)Time.frameCount);
                int writeIndex = (int)(frameIndex % RingCapacity);

                DebugLogEntry entry = default;
                entry.FrameIndex = frameIndex;
                entry.SystemMask = systemMask;
                entry.DeltaTime = dt;
                entry.FixedDeltaTime = _lastFixedDeltaTime;
                entry.GpuFrameTime = gpuFrameTime;
                entry.MemoryUsedMb = reservedMemoryMb;
                entry.PlayerAup = playerAup;
                entry.ActiveChunkCount = activeChunkCount;
                entry.ErrorFlags = errorFlags;
                entry.ExportReason = (uint)ExportReason.None;
                entry.AupShiftSequence = shiftEvent.Sequence;
                entry.AiStatePacked = PackAiState();
                entry.SubsystemHeatPacked = PackSubsystemHeat();
                entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
                _ringBuffer[writeIndex] = entry;
                TryWriteLiveTelemetry(frameIndex, dt, reservedMemoryMb, activeChunkCount);

                _writeCursor++;
                if (errorFlags != 0u)
                {
                    _stickyErrorFlags |= errorFlags;
                    bool forceNanExport = (errorFlags & (uint)ErrorBits.NanPhysics) != 0u;
                    TryExportSnapshot(ExportReason.ErrorFlags, errorFlags, writeSynchronously: forceNanExport);
                }
            }
        }

        /// <summary>
        /// Caches the fixed-step delta so it can be emitted alongside frame telemetry.
        /// </summary>
        /// <param name="fdt">Fixed delta passed by <see cref="GameTickManager"/>.</param>
        public void FixedTick(float fdt)
        {
            _lastFixedDeltaTime = fdt;
        }

        private void WriteBootstrapPhaseDuration(BootstrapStepToken step, double elapsedMilliseconds)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = (int)(frameIndex % RingCapacity);
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            DebugLogEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Bootstrap;
            entry.DeltaTime = 0f;
            entry.FixedDeltaTime = _lastFixedDeltaTime;
            entry.GpuFrameTime = (float)elapsedMilliseconds;
            entry.MemoryUsedMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
            entry.PlayerAup = float3.zero;
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.BootstrapPhaseDuration;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = PackBootstrapPhaseDuration(step, elapsedMilliseconds);
            entry.SubsystemHeatPacked = 0u;
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
            _writeCursor++;
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
            int writeIndex = (int)(frameIndex % RingCapacity);

            DebugLogEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.OriginShift;
            entry.DeltaTime = 0f;
            entry.FixedDeltaTime = _lastFixedDeltaTime;
            entry.GpuFrameTime = 0f;
            entry.MemoryUsedMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
            entry.PlayerAup = shift3;
            entry.ActiveChunkCount = SampleActiveChunkCount();
            entry.ErrorFlags = 0u;
            entry.ExportReason = (uint)ExportReason.None;
            entry.AupShiftSequence = shiftSequence;
            entry.AiStatePacked = PackAiState();
            entry.SubsystemHeatPacked = PackSubsystemHeat();
            entry.LastOriginShiftFrame = frameIndex;
            _ringBuffer[writeIndex] = entry;
            _writeCursor++;
        }

        private void WriteBusCongestionTelemetry(uint queueHash, int pendingCount, int entityCount)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = (int)(frameIndex % RingCapacity);
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;

            DebugLogEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.EventBus;
            entry.DeltaTime = Time.unscaledDeltaTime;
            entry.FixedDeltaTime = _lastFixedDeltaTime;
            entry.GpuFrameTime = (float)Time.unscaledTimeAsDouble;
            entry.MemoryUsedMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
            entry.PlayerAup = SamplePlayerPosition(out _);
            entry.ActiveChunkCount = unchecked((uint)math.max(0, entityCount));
            entry.ErrorFlags = (uint)ErrorBits.BusCongestionWarning;
            entry.ExportReason = (uint)ExportReason.BusCongestionWarning;
            entry.AupShiftSequence = shiftEvent.Sequence;
            entry.AiStatePacked = queueHash;
            entry.SubsystemHeatPacked = unchecked((uint)math.max(0, pendingCount));
            entry.LastOriginShiftFrame = unchecked((uint)math.max(0, shiftEvent.Frame));
            _ringBuffer[writeIndex] = entry;
            _writeCursor++;
            TryExportSnapshot(ExportReason.BusCongestionWarning, (uint)ErrorBits.BusCongestionWarning, writeSynchronously: false);
        }

        private void WriteKineticAnomalyTelemetry(Vector3 runtimePosition, Vector3 deltaVelocity, float accelerationMetersPerSecondSq)
        {
            uint frameIndex = unchecked((uint)Time.frameCount);
            int writeIndex = (int)(frameIndex % RingCapacity);
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            float3 absolutePosition = ToAbsoluteUniversePosition(runtimePosition);
            float3 deltaVelocity3 = new float3(deltaVelocity.x, deltaVelocity.y, deltaVelocity.z);
            if (!math.all(math.isfinite(absolutePosition)) || !math.all(math.isfinite(deltaVelocity3)))
                return;

            DebugLogEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.SystemMask = (uint)SystemBits.Physics;
            entry.DeltaTime = Time.unscaledDeltaTime;
            entry.FixedDeltaTime = _lastFixedDeltaTime;
            entry.GpuFrameTime = math.max(0f, accelerationMetersPerSecondSq);
            entry.MemoryUsedMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
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
            _writeCursor++;
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
                return;

            if (!UnsafeUtility.IsBlittable<DebugLogEntry>() ||
                UnsafeUtility.SizeOf<CrashExportHeader>() != CrashExportHeaderSizeBytes ||
                UnsafeUtility.SizeOf<DebugLogEntry>() != DebugLogEntrySizeBytes)
            {
                enabled = false;
                Debug.LogError("CrashTelemetryBuffer requires a blittable 16-byte header and a blittable 64-byte DebugLogEntry.");
                return;
            }

            // COLD ALLOC: NativeArray<DebugLogEntry>[300] - lockless telemetry ring buffer - owner: CrashTelemetryBuffer
            _ringBuffer = new NativeArray<DebugLogEntry>(RingCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _ringBuffer,
                nameof(CrashTelemetryBuffer),
                nameof(_ringBuffer),
                NativeAllocationLifetime.Session);

            // COLD ALLOC: NativeArray<DebugLogEntry>[300] - pre-crash binary export snapshot staging buffer - owner: CrashTelemetryBuffer
            _exportSnapshot = new NativeArray<DebugLogEntry>(ExportSnapshotEntries, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _exportSnapshot,
                nameof(CrashTelemetryBuffer),
                nameof(_exportSnapshot),
                NativeAllocationLifetime.Session);

            // COLD ALLOC: NativeArray<byte>[19216] - binary export scratch for 16B header + 300 x 64B entries - owner: CrashTelemetryBuffer
            _exportScratch = new NativeArray<byte>(ExportScratchSizeBytes, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _exportScratch,
                nameof(CrashTelemetryBuffer),
                nameof(_exportScratch),
                NativeAllocationLifetime.Session);

            _liveTelemetryPath = Path.Combine(Application.persistentDataPath, LiveTelemetryFileName);
            _crashTelemetryPath = Path.Combine(Application.persistentDataPath, CrashTelemetryFileName);
            InitializeLiveTelemetryMmf();
            InitializeCrashTelemetryMmf();
            MemoryBudgetTracker.Register(
                MemoryBudgetOwnerName,
                ((long)_ringBuffer.Length * DebugLogEntrySizeBytes) +
                ((long)_exportSnapshot.Length * DebugLogEntrySizeBytes) +
                _exportScratch.Length,
                PersistentMemoryBudgetBytes);
        }

        private void DisposeBuffers()
        {
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
            DisposeLiveTelemetryMmf();
            DisposeCrashTelemetryMmf();
            _liveTelemetryPath = null;
            _crashTelemetryPath = null;
            Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
            DisposeRecorder(ref _frameTimeRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
        }

        private void Subscribe()
        {
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.logMessageReceived += HandleLogMessageReceived;
            Application.logMessageReceivedThreaded += HandleLogMessageReceivedThreaded;
        }

        private void Unsubscribe()
        {
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            Application.logMessageReceived -= HandleLogMessageReceived;
            Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                _registeredTick = true;
            }

            if (!_registeredFixedTick)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Core);
                _registeredFixedTick = true;
            }
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

        private void ResolvePlayerTransform(float dt)
        {
            if (_playerTransform != null)
                return;

            _playerResolveCooldown -= dt;
            if (_playerResolveCooldown > 0f)
                return;

            _playerResolveCooldown = PlayerResolveCooldownSeconds;
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                _playerTransform = playerTransform;
                if (_survivalSystem == null && _playerTransform != null)
                    _playerTransform.TryGetComponent(out _survivalSystem);
            }
        }

        private static uint SampleSystemMask()
        {
            uint systemMask = 0u;
            if (HectonFluidEngine.Instance != null)
            {
                systemMask |= (uint)SystemBits.Physics;
                systemMask |= (uint)SystemBits.Fluid;
            }

            if (HectonVoxelEngine.ActiveRuntimeInstance != null)
                systemMask |= (uint)SystemBits.Voxel;

            if (HectonDirectorAI.ActiveRuntimeInstance != null)
                systemMask |= (uint)SystemBits.AI;

            return systemMask;
        }

        private static uint SampleActiveChunkCount()
        {
            HectonVoxelEngine voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;
            return voxelEngine != null
                ? unchecked((uint)Mathf.Max(0, voxelEngine.ActiveVolumeCount))
                : 0u;
        }

        private void ResolveProfilerRecorders()
        {
            _frameTimeRecorder = StartRecorder(_FrameTimeCandidates);
            _gcAllocRecorder = StartRecorder(_GcAllocCandidates);
        }

        private float3 SamplePlayerPosition(out bool hasPlayer)
        {
            if (_playerTransform == null)
            {
                hasPlayer = false;
                return float3.zero;
            }

            hasPlayer = true;
            Vector3 runtimePosition = _playerTransform.position;
            return ToAbsoluteUniversePosition(runtimePosition);
        }

        private static float3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            Vector3 bridgeUniversePosition = HectonMapMagicVegetationBridge.ToUniverseSpace(runtimePosition);
            Vector3 absolutePosition = bridgeUniversePosition != runtimePosition
                ? bridgeUniversePosition
                : HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            return new float3(absolutePosition.x, absolutePosition.y, absolutePosition.z);
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

            _stickyErrorFlags |= faultFlags;
            Interlocked.Exchange(ref _threadedFaultFlags, 0);
            TryExportSnapshot(exportReason, faultFlags, writeSynchronously: false);
        }

        private void HandleLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                OrThreadedFaultFlags((int)ErrorBits.ExceptionLogged);
            }
            else if (type == LogType.Error || type == LogType.Assert)
            {
                OrThreadedFaultFlags((int)ErrorBits.ErrorLogged);
            }
        }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
        {
            uint exportFlags = (uint)ErrorBits.ExceptionLogged;
            _stickyErrorFlags |= exportFlags;
            OrThreadedFaultFlags(unchecked((int)exportFlags));
            TryExportSnapshotFromUnhandledException(exportFlags);
        }

        private void OrThreadedFaultFlags(int flags)
        {
            int snapshot;
            int combined;
            do
            {
                snapshot = _threadedFaultFlags;
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
                snapshot = _runtimeFaultFlags;
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
            if (_survivalSystem == null && _playerTransform != null)
                _playerTransform.TryGetComponent(out _survivalSystem);

            float heatSeverity = _survivalSystem != null ? _survivalSystem.HeatStressSeverity01 : 0f;
            float environmentTemperature = _survivalSystem != null ? _survivalSystem.EnvironmentTemperature : 0f;
            float internalTemperature = _survivalSystem != null ? _survivalSystem.InternalTemperature : 0f;

            uint heat = QuantizeUnitToByte(heatSeverity);
            uint environment = QuantizeSignedTemperatureToByte(environmentTemperature);
            uint internalValue = QuantizeSignedTemperatureToByte(internalTemperature);
            uint thermalRuntimePresent = AbyssalThermalManager.Instance != null ? 1u : 0u;
            return heat |
                   (environment << 8) |
                   (internalValue << 16) |
                   (thermalRuntimePresent << 24);
        }

        private ProfilerRecorder StartRecorder(string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return default;

            _availableProfilerHandles.Clear();
            ProfilerRecorderHandle.GetAvailable(_availableProfilerHandles);
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

        private void InitializeLiveTelemetryMmf()
        {
            DisposeLiveTelemetryMmf();
            if (string.IsNullOrEmpty(_liveTelemetryPath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(_liveTelemetryPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                _liveTelemetryStream = new FileStream(_liveTelemetryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (_liveTelemetryStream.Length != LiveTelemetryRecordSizeBytes)
                    _liveTelemetryStream.SetLength(LiveTelemetryRecordSizeBytes);

                _liveTelemetryMmf = MemoryMappedFile.CreateFromFile(
                    _liveTelemetryStream,
                    null,
                    LiveTelemetryRecordSizeBytes,
                    MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None,
                    leaveOpen: true);
                _liveTelemetryView = _liveTelemetryMmf.CreateViewAccessor(0L, LiveTelemetryRecordSizeBytes, MemoryMappedFileAccess.Write);

                Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
            }
            catch (Exception exception)
            {
                DisposeLiveTelemetryMmf();
                _liveTelemetryPath = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
        }

        private void DisposeLiveTelemetryMmf()
        {
            Volatile.Write(ref _liveTelemetryWriteState, LiveTelemetryStateIdle);
            _liveTelemetryView?.Dispose();
            _liveTelemetryView = null;
            _liveTelemetryMmf?.Dispose();
            _liveTelemetryMmf = null;
            _liveTelemetryStream?.Dispose();
            _liveTelemetryStream = null;
        }

        private void InitializeCrashTelemetryMmf()
        {
            DisposeCrashTelemetryMmf();
            if (string.IsNullOrEmpty(_crashTelemetryPath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(_crashTelemetryPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                _crashTelemetryStream = new FileStream(_crashTelemetryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (_crashTelemetryStream.Length != ExportScratchSizeBytes)
                    _crashTelemetryStream.SetLength(ExportScratchSizeBytes);

                _crashTelemetryMmf = MemoryMappedFile.CreateFromFile(
                    _crashTelemetryStream,
                    null,
                    ExportScratchSizeBytes,
                    MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None,
                    leaveOpen: true);
                _crashTelemetryView = _crashTelemetryMmf.CreateViewAccessor(0L, ExportScratchSizeBytes, MemoryMappedFileAccess.Write);
            }
            catch (Exception exception)
            {
                DisposeCrashTelemetryMmf();
                _crashTelemetryPath = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
        }

        private void DisposeCrashTelemetryMmf()
        {
            Volatile.Write(ref _exportState, ExportStateIdle);
            _crashTelemetryView?.Dispose();
            _crashTelemetryView = null;
            _crashTelemetryMmf?.Dispose();
            _crashTelemetryMmf = null;
            _crashTelemetryStream?.Dispose();
            _crashTelemetryStream = null;
        }

        private void TryWriteLiveTelemetry(uint frameIndex, float dt, float reservedMemoryMb, uint activeChunkCount)
        {
            if (string.IsNullOrEmpty(_liveTelemetryPath))
                return;

            int frameNumber = unchecked((int)frameIndex);
            if (frameNumber <= 0 || frameNumber - _lastLiveTelemetryWriteFrame < LiveTelemetryWriteIntervalFrames)
                return;

            _lastLiveTelemetryWriteFrame = frameNumber;
            LiveTelemetryRecord record = default;
            record.Magic = LiveTelemetryMagic;
            record.Version = LiveTelemetryVersion;
            record.FrameIndex = frameIndex;
            record.ActiveChunkCount = activeChunkCount;
            record.GcAllocBytes = unchecked((uint)Mathf.Max(0, ReadIntValue(_gcAllocRecorder)));
            record.CpuFrameTimeMs = ReadMilliseconds(_frameTimeRecorder);
            record.DeltaTime = dt;
            record.ReservedMemoryMb = reservedMemoryMb;

            if (Interlocked.CompareExchange(ref _liveTelemetryWriteState, LiveTelemetryStateQueued, LiveTelemetryStateIdle) != LiveTelemetryStateIdle)
                return;

            _pendingLiveTelemetryRecord = record;
            ThreadPool.UnsafeQueueUserWorkItem(_backgroundLiveTelemetryCallback, this);
        }

        private static void ExecuteBackgroundLiveTelemetryWrite(object state)
        {
            if (state is CrashTelemetryBuffer crashTelemetryBuffer)
                crashTelemetryBuffer.WritePendingLiveTelemetryToMmf();
        }

        private unsafe void WritePendingLiveTelemetryToMmf()
        {
            try
            {
                if (_liveTelemetryView == null)
                    return;

                LiveTelemetryRecord record = _pendingLiveTelemetryRecord;
                byte* mappedBaseAddress = null;
                try
                {
                    _liveTelemetryView.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedBaseAddress);
                    if (mappedBaseAddress == null)
                        return;

                    byte* destination = mappedBaseAddress + (int)_liveTelemetryView.PointerOffset;
                    UnsafeUtility.MemClear(destination, LiveTelemetryRecordSizeBytes);
                    UnsafeUtility.CopyStructureToPtr(ref record, destination);
                    _liveTelemetryView.Flush();
                    _liveTelemetryStream?.Flush(true);
                }
                finally
                {
                    if (mappedBaseAddress != null)
                        _liveTelemetryView.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
            catch (UnauthorizedAccessException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            catch (IOException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
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

        private void TryExportSnapshot(ExportReason exportReason, uint exportFlags, bool writeSynchronously)
        {
            if (!_ringBuffer.IsCreated)
                return;

            if (Interlocked.CompareExchange(ref _exportState, ExportStateQueued, ExportStateIdle) != ExportStateIdle)
            {
                if ((exportFlags & (uint)ErrorBits.NanPhysics) != 0u)
                    OrRuntimeFaultFlags(unchecked((int)exportFlags));

                return;
            }

            bool exportQueued = false;
            try
            {
                int currentFrame = Time.frameCount;
                bool bypassCooldown = (exportFlags & (uint)ErrorBits.NanPhysics) != 0u;
                if (!writeSynchronously && !bypassCooldown && currentFrame - _lastExportFrame < ExportCooldownFrames)
                    return;

                using (ProfilerRegistry.TelemetryExport.Auto())
                {
                    int snapshotCount = SnapshotRecentEntries(exportReason);
                    if (snapshotCount <= 0)
                        return;

                    _lastExportFrame = currentFrame;
                    _pendingExportBytes = BuildExportScratch(snapshotCount);
                    PreparePendingExportMetadata(unchecked((uint)math.max(0, currentFrame)));

                    if (writeSynchronously)
                    {
                        GlobalTelemetryBus.TryEmergencyFlushSynchronous();
                        WritePreparedExportToDisk();
                        return;
                    }

                    ThreadPool.UnsafeQueueUserWorkItem(_backgroundExportCallback, this);
                    exportQueued = true;
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
                return;

            try
            {
                int snapshotCount = SnapshotRecentEntries(ExportReason.AppDomainUnhandledException);
                if (snapshotCount <= 0)
                    return;

                _pendingExportBytes = BuildExportScratch(snapshotCount);
                uint triggerFrame = _writeCursor <= 0L
                    ? 0u
                    : unchecked((uint)math.min(_writeCursor, uint.MaxValue));
                PreparePendingExportMetadata(triggerFrame);
                GlobalTelemetryBus.TryEmergencyFlushSynchronous();
                WritePreparedExportToDisk();
            }
            catch (Exception)
            {
                Volatile.Write(ref _exportState, ExportStateIdle);
            }
        }

        private int SnapshotRecentEntries(ExportReason exportReason)
        {
            if (!_ringBuffer.IsCreated || !_exportSnapshot.IsCreated)
                return 0;

            long committedEntries = math.min(_writeCursor, RingCapacity);
            long skipNewestEntry = exportReason == ExportReason.ErrorFlags ? 1L : 0L;
            long availableEntries = math.min(ExportSnapshotEntries, committedEntries - skipNewestEntry);
            if (availableEntries <= 0)
                return 0;

            long startCursor = _writeCursor - skipNewestEntry - availableEntries;
            for (int i = 0; i < availableEntries; i++)
            {
                int ringIndex = (int)((startCursor + i) % RingCapacity);
                _exportSnapshot[i] = _ringBuffer[ringIndex];
            }

            return (int)availableEntries;
        }

        private int BuildExportScratch(int snapshotCount)
        {
            unsafe
            {
                CrashExportHeader header = default;
                header.Magic = BinaryMagic;
                header.EntryCount = unchecked((uint)snapshotCount);
                header.StructSizeBytes = DebugLogEntrySizeBytes;

                int entryBytes = snapshotCount * DebugLogEntrySizeBytes;
                int totalBytes = CrashExportHeaderSizeBytes + entryBytes;

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_exportScratch);
                UnsafeUtility.MemClear(destination, totalBytes);
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

        private void PreparePendingExportMetadata(uint triggerFrame)
        {
            _pendingExportPath = _crashTelemetryPath;
        }

        private static void ExecuteBackgroundExport(object state)
        {
            if (state is CrashTelemetryBuffer crashTelemetryBuffer)
                crashTelemetryBuffer.WritePreparedExportToDisk();
        }

        private void WritePreparedExportToDisk()
        {
            try
            {
                int exportBytes = _pendingExportBytes;
                if (_crashTelemetryView != null && exportBytes > 0)
                {
                    unsafe
                    {
                        byte* mappedBaseAddress = null;
                        try
                        {
                            _crashTelemetryView.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedBaseAddress);
                            if (mappedBaseAddress == null)
                                return;

                            byte* destination = mappedBaseAddress + (int)_crashTelemetryView.PointerOffset;
                            UnsafeUtility.MemClear(destination, ExportScratchSizeBytes);
                            void* exportPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_exportScratch);
                            if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, ExportScratchSizeBytes, exportPtr, exportBytes))
                            {
                                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(CrashTelemetryBuffer));
                                return;
                            }

                            _crashTelemetryView.Flush();
                            _crashTelemetryStream?.Flush(true);
                        }
                        finally
                        {
                            if (mappedBaseAddress != null)
                                _crashTelemetryView.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            catch (IOException exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            catch (Exception)
            {
            }
            finally
            {
                _pendingExportBytes = 0;
                _pendingExportPath = null;
                Volatile.Write(ref _exportState, ExportStateIdle);
            }
        }

        private static float ReadMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0f;

            if (recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds)
                return (float)(recorder.LastValue / 1000000.0d);

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

        private static bool MatchesCandidate(string value, string candidate)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(candidate))
                return false;

            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase) ||
                   value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static uint QuantizeUnitToByte(float value)
        {
            return unchecked((uint)math.clamp(math.round(math.saturate(value) * 255f), 0f, 255f));
        }

        private static uint QuantizeSignedTemperatureToByte(float temperatureCelsius)
        {
            float normalized = math.saturate((temperatureCelsius + 50f) / 150f);
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
