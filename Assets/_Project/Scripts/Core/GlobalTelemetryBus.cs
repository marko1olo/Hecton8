using System;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.SaveSystem;
using Hecton.Localization;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public enum TelemetryEventType : byte
    {
        PlayerDeath = 0,
        BiomeVisited = 1,
        ItemCrafted = 2,
        BootstrapDependencyCycle = 3,
        JobBarrierStall = 4,
        PoolExhausted = 5,
        DependencyOrderWarning = 6,
        InteractionPacketOverflow = 7,
        ModTelemetry = 8,
        ModStallWarning = 9,
        ModCommandRejected = 10,
        DroneFleetStatus = 11,
        ModCriticalMemoryEviction = 12,
        PerformanceWarning = 13,
        BootstrapDuration = 14,
        CatastrophicCascadePrevented = 15,
        CriticalGcSpike = 16,
        PerformanceSpike = 17,
        MathGuardInvalidNumber = 18,
        VRAMWarning = 19,
        InputLagWarning = 20,
        SystemDegradation = 21,
        DrawCallEstimate = 22,
        ShaderFallback = 23,
        RegistryHeartbeatStale = 24,
        MemoryBreach = 25
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct TelemetryEvent
    {
        public uint FrameIndex;
        public uint EventType;
        public uint SubjectHash;
        public uint ContextHash;
        public float ScalarValue;
        public float3 WorldPosition;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
        public uint Reserved5;
        public uint Reserved6;
        public uint Reserved7;
    }

    public static class GlobalTelemetryBus
    {
        private const int RetainedFrameCount = 1000;
        private const int Capacity = 1024;
        private const int CapacityMask = Capacity - 1;
        private const int BinaryHeaderSizeBytes = 16;
        private const int Version = 1;
        private const uint BinaryMagic = 0x4D4C4554u; // "TELM"
        private const uint InputLagWarningHash = 0x494C4147u; // "ILAG"
        private const float DrainIntervalSeconds = 60f;
        private const int SnapshotCopyBudgetPerLateFrame = 128;
        private const string ExportFolderName = "Telemetry";
        private const string BinaryExtension = ".h8dump";
        private const string ExportTimestampFormat = "yyyyMMdd_HHmmss_fffffff";
        private const string TelemetryFileNamePrefix = "telemetry_";
        private const int ExportTimestampCharCount = 23;
        private const int ExportWorkerJoinMilliseconds = 250;
        private const int ExportRequestNone = 0;
        private const int ExportRequestPrepared = 1;
        private const int ExportRequestEmergency = 2;
        private const string ExportThreadName = "H8.TelemetryExport";
        public const float BytesToMegabytes = 0.000000953674f;

        private static NativeRingBuffer<TelemetryEvent> _ringBuffer;
        private static NativeArray<TelemetryEvent> _snapshotBuffer;
        private static NativeArray<byte> _exportScratch;
        private static long _writeCursor;
        private static float _nextDrainTimeSeconds = DrainIntervalSeconds;
        private static int _exportInFlight;
        private static int _mmfWriteInProgress;
        private static int _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        private static int _pendingEventCount;
        private static int _pendingByteCount;
        private static string _pendingBinaryPath;
        private static string _pendingTelemetryDirectory;
        private static long _pendingGeneratedUtcTicks;
        private static bool _snapshotInProgress;
        private static long _snapshotStartIndex;
        private static int _snapshotTotalCount;
        private static int _snapshotCopiedCount;
        private static long _nativeCopyByteCount;
        private static int _nativeCopyOperationCount;
        private static int _pendingExportRequest;
        private static int _exportStopRequested;
        private static AutoResetEvent _exportSignal;
        private static Thread _exportThread;
        // COLD ALLOC: object[1] — first-use native telemetry buffer initialization gate — owner: GlobalTelemetryBus
        private static readonly object _initGate = new object();
        // COLD ALLOC: object[1] - dedicated telemetry export worker lifecycle gate - owner: GlobalTelemetryBus
        private static readonly object _exportThreadGate = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeStaticState();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorLifecycleHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeStaticState;
            UnityEditor.EditorApplication.quitting -= DisposeStaticState;
            UnityEditor.EditorApplication.quitting += DisposeStaticState;
        }
#endif

        private static void DisposeStaticState()
        {
            StopExportThread();
            lock (_initGate)
            {
                bool snapshotCopyInProgress = _snapshotInProgress;
                bool writerOwnsExportState =
                    Volatile.Read(ref _mmfWriteInProgress) != 0 ||
                    (Volatile.Read(ref _exportInFlight) != 0 && !snapshotCopyInProgress);
                JobHandle noDependency = default;
                DisposeRingBuffer(ref _ringBuffer);
                DisposeNativeArray(ref _snapshotBuffer, noDependency);

                _writeCursor = 0;
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _nextDrainTimeSeconds = DrainIntervalSeconds;
                _snapshotInProgress = false;
                _snapshotStartIndex = 0;
                _snapshotTotalCount = 0;
                _snapshotCopiedCount = 0;

                if (!writerOwnsExportState)
                {
                    DisposeNativeArray(ref _exportScratch, noDependency);
                    _exportInFlight = 0;
                    _mmfWriteInProgress = 0;
                    _pendingExportRequest = ExportRequestNone;
                    ClearPendingExportState(clearDirectory: true);
                }
            }

            Interlocked.Exchange(ref _nativeCopyByteCount, 0L);
            Interlocked.Exchange(ref _nativeCopyOperationCount, 0);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private static void DisposeRingBuffer(ref NativeRingBuffer<TelemetryEvent> buffer)
        {
            if (!buffer.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(buffer.RawArray);
            buffer.Dispose();
            buffer = default;
        }

        public static void PublishPlayerDeath(Vector3 worldPosition)
        {
            Publish(TelemetryEventType.PlayerDeath, 0u, 0u, 0f, worldPosition);
        }

        /// <summary>
        /// Cold-allocates the telemetry buffers before runtime systems can publish failure events.
        /// </summary>
        public static void Initialize()
        {
            EnsureInitialized();
        }

        public static void PublishBiomeVisited(string biomeId, int depthTier, float depthMeters)
        {
            Publish(
                TelemetryEventType.BiomeVisited,
                ComputeHash(biomeId),
                unchecked((uint)depthTier),
                depthMeters,
                default);
        }

        public static void PublishItemCrafted(string itemPersistentId)
        {
            Publish(TelemetryEventType.ItemCrafted, ComputeHash(itemPersistentId), 0u, 1f, default);
        }

        public static void PublishItemCrafted(uint itemHashId)
        {
            Publish(TelemetryEventType.ItemCrafted, itemHashId, 0u, 1f, default);
        }

        public static void PublishBootstrapDependencyCycle(string serviceId, string dependencyId)
        {
            Publish(
                TelemetryEventType.BootstrapDependencyCycle,
                ComputeHash(serviceId),
                ComputeHash(dependencyId),
                0f,
                default);
        }

        public static void PublishJobBarrierStall(string systemName, string phaseName, float stallMilliseconds)
        {
            Publish(
                TelemetryEventType.JobBarrierStall,
                ComputeHash(systemName),
                ComputeHash(phaseName),
                stallMilliseconds,
                default);
        }

        /// <summary>
        /// Publishes a pool exhaustion event without requiring a managed prefab name payload.
        /// </summary>
        /// <param name="prefabId">Registered prefab identifier that failed to spawn.</param>
        /// <param name="reasonCode">Pool failure reason code owned by <see cref="ObjectPoolManager"/>.</param>
        public static void PublishPoolExhausted(int prefabId, uint reasonCode)
        {
            Publish(
                TelemetryEventType.PoolExhausted,
                unchecked((uint)prefabId),
                reasonCode,
                1f,
                default);
        }

        /// <summary>
        /// Publishes a dependency-order warning using precomputed stable hashes.
        /// </summary>
        /// <param name="serviceHash">Service slot hash that was accessed before registration.</param>
        /// <param name="requesterHash">Optional requester hash. Zero when unknown.</param>
        public static void PublishDependencyOrderWarning(uint serviceHash, uint requesterHash)
        {
            Publish(
                TelemetryEventType.DependencyOrderWarning,
                serviceHash,
                requesterHash,
                1f,
                default);
        }

        /// <summary>
        /// Publishes a dropped interaction-packet overflow event without managed payload text.
        /// </summary>
        /// <param name="frameCapacity">Per-frame interaction packet capacity.</param>
        /// <param name="queuedCount">Current queued interaction signal count.</param>
        public static void PublishInteractionPacketOverflow(int frameCapacity, int queuedCount)
        {
            Publish(
                TelemetryEventType.InteractionPacketOverflow,
                unchecked((uint)frameCapacity),
                unchecked((uint)math.max(0, queuedCount)),
                1f,
                default);
        }

        /// <summary>
        /// Publishes a mod-owned telemetry marker using precomputed hashes only.
        /// </summary>
        /// <param name="modHash">Stable mod hash.</param>
        /// <param name="markerHash">Stable marker hash.</param>
        /// <param name="scalarValue">Optional scalar payload.</param>
        public static void PublishModTelemetry(uint modHash, uint markerHash, float scalarValue)
        {
            Publish(TelemetryEventType.ModTelemetry, modHash, markerHash, scalarValue, default);
        }

        /// <summary>
        /// Publishes a mod callback stall marker.
        /// </summary>
        /// <param name="modHash">Stable mod hash.</param>
        /// <param name="eventHash">Stable event or lane hash.</param>
        /// <param name="stallMilliseconds">Measured callback time.</param>
        public static void PublishModStallWarning(uint modHash, uint eventHash, float stallMilliseconds)
        {
            Publish(TelemetryEventType.ModStallWarning, modHash, eventHash, stallMilliseconds, default);
        }

        /// <summary>
        /// Publishes a rejected mod command marker.
        /// </summary>
        /// <param name="modHash">Stable mod hash.</param>
        /// <param name="reasonCode">Reject reason code.</param>
        /// <param name="count">Rejected command count.</param>
        public static void PublishModCommandRejected(uint modHash, uint reasonCode, float count)
        {
            Publish(TelemetryEventType.ModCommandRejected, modHash, reasonCode, count, default);
        }

        /// <summary>
        /// Publishes a generic performance warning using precomputed stable hashes only.
        /// </summary>
        public static void PublishPerformanceWarning(uint warningHash, uint contextHash, float scalarValue)
        {
            Publish(TelemetryEventType.PerformanceWarning, warningHash, contextHash, scalarValue, default);
        }

        /// <summary>
        /// Publishes a post-warmup Gen0 collection spike using precomputed stable hashes only.
        /// </summary>
        public static void PublishCriticalGcSpike(uint spikeHash, uint contextHash, int gen0CollectionsDelta)
        {
            Publish(
                TelemetryEventType.CriticalGcSpike,
                spikeHash,
                contextHash,
                math.max(1f, gen0CollectionsDelta),
                default);
        }

        /// <summary>
        /// Publishes a frame-time watchdog spike using precomputed subsystem hashes only.
        /// </summary>
        /// <param name="subsystemHash">Subsystem hash with the highest reported cost this frame.</param>
        /// <param name="frameTimeMilliseconds">Observed frame duration in milliseconds.</param>
        public static void PublishPerformanceSpike(uint subsystemHash, float frameTimeMilliseconds)
        {
            Publish(TelemetryEventType.PerformanceSpike, subsystemHash, 0u, frameTimeMilliseconds, default);
        }

        /// <summary>
        /// Publishes a Burst math invalid-number guard event.
        /// </summary>
        /// <param name="errorCode">Caller-owned deterministic error code.</param>
        public static void PublishMathGuardInvalidNumber(int errorCode)
        {
            Publish(TelemetryEventType.MathGuardInvalidNumber, unchecked((uint)errorCode), 0u, 1f, default);
        }

        /// <summary>
        /// Publishes an estimated VRAM budget warning.
        /// </summary>
        /// <param name="estimatedBytes">Current estimated payload in bytes.</param>
        public static void PublishVRAMWarningEvent(long estimatedBytes)
        {
            float estimatedMegabytes = estimatedBytes * BytesToMegabytes;
            Publish(TelemetryEventType.VRAMWarning, 0u, 0u, estimatedMegabytes, default);
        }

        /// <summary>
        /// Publishes an input-to-render latency warning.
        /// </summary>
        /// <param name="latencyMilliseconds">Observed completed latency in milliseconds.</param>
        public static void PublishInputLagWarning(float latencyMilliseconds)
        {
            Publish(TelemetryEventType.InputLagWarning, InputLagWarningHash, 0u, latencyMilliseconds, default);
        }

        /// <summary>
        /// Publishes a sustained frame-time degradation action using numeric payloads only.
        /// </summary>
        public static void PublishSystemDegradation(uint reasonHash, uint actionMask, float frameTimeMilliseconds)
        {
            Publish(TelemetryEventType.SystemDegradation, reasonHash, actionMask, frameTimeMilliseconds, default);
        }

        /// <summary>
        /// Publishes an approximate BRG-backed draw-call count reported by render managers.
        /// </summary>
        public static void PublishApproximateDrawCallCount(int batchCount)
        {
            Publish(TelemetryEventType.DrawCallEstimate, unchecked((uint)math.max(0, batchCount)), 0u, batchCount, default);
        }

        /// <summary>
        /// Publishes a shader fallback swap without material names or paths.
        /// </summary>
        public static void PublishShaderFallback(uint materialHash, uint shaderHash, float count)
        {
            Publish(TelemetryEventType.ShaderFallback, materialHash, shaderHash, math.max(1f, count), default);
        }

        /// <summary>
        /// Publishes a stale registry heartbeat by numeric service slot only.
        /// </summary>
        public static void PublishRegistryHeartbeatStale(uint serviceSlot, uint tickCount)
        {
            Publish(TelemetryEventType.RegistryHeartbeatStale, serviceSlot, tickCount, 1f, default);
        }

        /// <summary>
        /// Broadcasts a memory subsystem breach as numeric HUD/black-box payload only.
        /// </summary>
        public static void PublishMemoryBreachEvent(uint contextHash, float currentMegabytes)
        {
            Publish(TelemetryEventType.MemoryBreach, contextHash, 0u, math.max(0f, currentMegabytes), default);
        }

        /// <summary>
        /// Publishes a BaseEvents cascade breaker trip using numeric IslandID/event hashes only.
        /// </summary>
        public static void PublishCatastrophicCascadePrevented(uint islandId, uint eventHash, int droppedCount)
        {
            Publish(
                TelemetryEventType.CatastrophicCascadePrevented,
                islandId,
                eventHash,
                math.max(1f, droppedCount),
                default);
        }

        /// <summary>
        /// Publishes total bootstrap duration using precomputed stable hashes only.
        /// </summary>
        public static void PublishBootstrapDuration(uint durationHash, uint contextHash, float elapsedMilliseconds)
        {
            Publish(TelemetryEventType.BootstrapDuration, durationHash, contextHash, elapsedMilliseconds, default);
        }

        /// <summary>
        /// Publishes a critical mod memory eviction marker.
        /// </summary>
        /// <param name="modHash">Stable mod hash.</param>
        /// <param name="trackedHeapBytes">Tracked managed allocation bytes charged to the mod.</param>
        /// <param name="quotaBytes">Configured quota in bytes.</param>
        public static void PublishModCriticalMemoryEviction(uint modHash, long trackedHeapBytes, long quotaBytes)
        {
            uint context = unchecked((uint)math.min(uint.MaxValue, math.max(0L, quotaBytes)));
            float scalar = math.max(0f, trackedHeapBytes * BytesToMegabytes);
            Publish(TelemetryEventType.ModCriticalMemoryEviction, modHash, context, scalar, default);
        }

        /// <summary>
        /// Publishes the headless drone fleet status using numeric payloads only.
        /// </summary>
        public static void PublishDroneFleetStatus(
            int totalActive,
            float averageBattery,
            int solderReserve,
            int lostUnits,
            int hostileUnits)
        {
            uint subject = unchecked((uint)math.max(0, totalActive));
            uint context = unchecked((uint)((math.min(65535, math.max(0, solderReserve)) << 16) |
                                            math.min(65535, math.max(0, hostileUnits))));
            float scalar = math.max(0f, averageBattery) + (math.max(0, lostUnits) * 0.001f);
            Publish(TelemetryEventType.DroneFleetStatus, subject, context, scalar, default);
        }

        public static void LateFrameUpdate(float unscaledTimeSeconds)
        {
            if (!_ringBuffer.IsCreated)
                return;

            if (_snapshotInProgress)
            {
                ContinueSnapshotCopy();
                return;
            }

            if (unscaledTimeSeconds < _nextDrainTimeSeconds)
                return;

            _nextDrainTimeSeconds = unscaledTimeSeconds + DrainIntervalSeconds;
            BeginSnapshotCopy();
        }

        /// <summary>
        /// Records guarded native copy throughput for memory heatmap telemetry.
        /// </summary>
        /// <param name="byteCount">Copied byte count.</param>
        [BurstDiscard]
        public static void RecordNativeCopy(long byteCount)
        {
            if (byteCount <= 0L)
                return;

            Interlocked.Increment(ref _nativeCopyOperationCount);
            Interlocked.Add(ref _nativeCopyByteCount, byteCount);
        }

        /// <summary>
        /// Total bytes copied through <see cref="UnsafeMemoryCopyGuard"/> since the last subsystem reset.
        /// </summary>
        public static long NativeCopyByteCount => Interlocked.Read(ref _nativeCopyByteCount);

        /// <summary>
        /// Total guarded copy operations since the last subsystem reset.
        /// </summary>
        public static int NativeCopyOperationCount => Volatile.Read(ref _nativeCopyOperationCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static bool IsInitializedForSmoke => _ringBuffer.IsCreated;

        internal static void ResetForSmokeTest()
        {
            DisposeStaticState();
        }
#endif

        /// <summary>
        /// Total whole megabytes copied through <see cref="UnsafeMemoryCopyGuard"/> since the last subsystem reset.
        /// </summary>
        public static long NativeCopyMegabyteCount => Interlocked.Read(ref _nativeCopyByteCount) >> 20;

        /// <summary>
        /// Compatibility shim for legacy crash handlers. Caller-thread MMF flush is forbidden;
        /// this only queues the background emergency flush path.
        /// </summary>
        public static bool TryEmergencyFlushSynchronous()
        {
            RequestEmergencyFlushAsync();
            return false;
        }

        /// <summary>
        /// Queues a raw full-ring crash dump from a non-main-thread log callback.
        /// </summary>
        public static void RequestEmergencyFlushAsync()
        {
            if (!_ringBuffer.IsCreated ||
                !_snapshotBuffer.IsCreated ||
                !_exportScratch.IsCreated ||
                string.IsNullOrEmpty(_pendingTelemetryDirectory))
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return;

            Volatile.Write(ref _pendingExportRequest, ExportRequestEmergency);
            if (!SignalExportThread())
            {
                Volatile.Write(ref _pendingExportRequest, ExportRequestNone);
                Interlocked.Exchange(ref _exportInFlight, 0);
            }
        }

        /// <summary>
        /// Attempts a background-thread raw full-ring dump without touching Unity path APIs.
        /// </summary>
        public static bool TryEmergencyFlushFromBackground()
        {
            if (!_ringBuffer.IsCreated ||
                !_snapshotBuffer.IsCreated ||
                !_exportScratch.IsCreated ||
                string.IsNullOrEmpty(_pendingTelemetryDirectory))
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return false;

            Volatile.Write(ref _pendingExportRequest, ExportRequestEmergency);
            if (SignalExportThread())
            {
                return true;
            }

            Volatile.Write(ref _pendingExportRequest, ExportRequestNone);
            Interlocked.Exchange(ref _exportInFlight, 0);
            return false;
        }

        private static bool TryEmergencyFlushLocked()
        {
            if (!_ringBuffer.IsCreated || !_snapshotBuffer.IsCreated)
                return false;

            long writeCursor = Volatile.Read(ref _writeCursor);
            int totalWritten = writeCursor >= RetainedFrameCount
                ? RetainedFrameCount
                : (int)(writeCursor > 0L ? writeCursor : 0L);
            if (totalWritten <= 0)
                return false;

            CopySnapshotUnbounded(writeCursor - totalWritten, totalWritten);
            if (!PrepareExportState(totalWritten, DateTime.UtcNow.Ticks))
                return false;

            return WritePreparedExportToMmf();
        }

        private static void Publish(
            TelemetryEventType eventType,
            uint subjectHash,
            uint contextHash,
            float scalarValue,
            Vector3 worldPosition)
        {
            bool isMainThread = Thread.CurrentThread.ManagedThreadId == _mainThreadId;
            if (isMainThread)
            {
                if (!Application.isPlaying)
                    return;

                EnsureInitialized();
            }
            else if (!_ringBuffer.IsCreated)
            {
                return;
            }

            TelemetryEvent telemetryEvent = new TelemetryEvent
            {
                FrameIndex = isMainThread ? unchecked((uint)Time.frameCount) : 0u,
                EventType = (uint)eventType,
                SubjectHash = subjectHash,
                ContextHash = contextHash,
                ScalarValue = scalarValue,
                WorldPosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z)
            };

            _ringBuffer.Write(in telemetryEvent);
            Volatile.Write(ref _writeCursor, _ringBuffer.TotalWrites);
        }

        private static void EnsureInitialized()
        {
            if (_ringBuffer.IsCreated &&
                _snapshotBuffer.IsCreated &&
                _exportScratch.IsCreated &&
                !string.IsNullOrEmpty(_pendingTelemetryDirectory))
            {
                return;
            }

            lock (_initGate)
            {
                if (!_ringBuffer.IsCreated)
                {
                    _ringBuffer = new NativeRingBuffer<TelemetryEvent>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<TelemetryEvent>[1024] — power-of-two black-box ring retaining the last 1000 telemetry frames — owner: GlobalTelemetryBus
                    NativeMemorySentinel.RegisterNativeArray(
                        _ringBuffer.RawArray,
                        nameof(GlobalTelemetryBus),
                        nameof(_ringBuffer),
                        NativeAllocationLifetime.Session);
                }

                if (!_snapshotBuffer.IsCreated)
                {
                    _snapshotInProgress = false;
                    _snapshotStartIndex = 0;
                    _snapshotTotalCount = 0;
                    _snapshotCopiedCount = 0;
                    _snapshotBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] — telemetry export snapshot staging buffer — owner: GlobalTelemetryBus
                    NativeMemorySentinel.RegisterNativeArray(
                        _snapshotBuffer,
                        nameof(GlobalTelemetryBus),
                        nameof(_snapshotBuffer),
                        NativeAllocationLifetime.Session);
                }

                if (!_exportScratch.IsCreated)
                {
                    int exportScratchBytes = (Capacity * UnsafeUtility.SizeOf<TelemetryEvent>()) + BinaryHeaderSizeBytes;
                    _exportScratch = new NativeArray<byte>(exportScratchBytes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[65552] — unmanaged binary telemetry export scratch — owner: GlobalTelemetryBus
                    NativeMemorySentinel.RegisterNativeArray(
                        _exportScratch,
                        nameof(GlobalTelemetryBus),
                        nameof(_exportScratch),
                        NativeAllocationLifetime.Session);
                }

                if (string.IsNullOrEmpty(_pendingTelemetryDirectory))
                    _pendingTelemetryDirectory = Path.Combine(Application.persistentDataPath, ExportFolderName);

                StartExportThread();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeHash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
        }

        private static void BeginSnapshotCopy()
        {
            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return;

            long writeCursor = Volatile.Read(ref _writeCursor);
            int totalWritten = writeCursor >= RetainedFrameCount
                ? RetainedFrameCount
                : (int)(writeCursor > 0L ? writeCursor : 0L);
            if (totalWritten <= 0)
            {
                Interlocked.Exchange(ref _exportInFlight, 0);
                return;
            }

            _snapshotStartIndex = writeCursor - totalWritten;
            _snapshotTotalCount = totalWritten;
            _snapshotCopiedCount = 0;
            _snapshotInProgress = true;
            ContinueSnapshotCopy();
        }

        private static void ContinueSnapshotCopy()
        {
            if (!_snapshotInProgress)
                return;
            if (!_ringBuffer.IsCreated || !_snapshotBuffer.IsCreated)
            {
                AbortPendingSnapshotCopy();
                return;
            }

            int remaining = _snapshotTotalCount - _snapshotCopiedCount;
            int copyCount = math.min(remaining, SnapshotCopyBudgetPerLateFrame);
            for (int i = 0; i < copyCount; i++)
            {
                int ringIndex = (int)(_snapshotStartIndex + _snapshotCopiedCount + i) & CapacityMask;
                _snapshotBuffer[_snapshotCopiedCount + i] = _ringBuffer[ringIndex];
            }

            _snapshotCopiedCount += copyCount;
            if (_snapshotCopiedCount < _snapshotTotalCount)
                return;

            CompleteSnapshotCopy();
        }

        private static void CopySnapshotUnbounded(long startIndex, int totalCount)
        {
            for (int i = 0; i < totalCount; i++)
            {
                int ringIndex = (int)(startIndex + i) & CapacityMask;
                _snapshotBuffer[i] = _ringBuffer[ringIndex];
            }
        }

        private static void CompleteSnapshotCopy()
        {
            try
            {
                if (!PrepareExportState(_snapshotTotalCount, DateTime.UtcNow.Ticks))
                {
                    AbortPendingSnapshotCopy();
                    return;
                }

                _snapshotInProgress = false;
                _snapshotStartIndex = 0;
                _snapshotTotalCount = 0;
                _snapshotCopiedCount = 0;
                Volatile.Write(ref _pendingExportRequest, ExportRequestPrepared);
                if (!SignalExportThread())
                {
                    Volatile.Write(ref _pendingExportRequest, ExportRequestNone);
                    ClearPendingExportState();
                    Interlocked.Exchange(ref _exportInFlight, 0);
                }
            }
            catch (Exception)
            {
                AbortPendingSnapshotCopy();
            }
        }

        private static void AbortPendingSnapshotCopy()
        {
            _snapshotInProgress = false;
            _snapshotStartIndex = 0;
            _snapshotTotalCount = 0;
            _snapshotCopiedCount = 0;
            ClearPendingExportState();
            Volatile.Write(ref _pendingExportRequest, ExportRequestNone);
            Interlocked.Exchange(ref _exportInFlight, 0);
        }

        private static bool PrepareExportState(int eventCount, long generatedUtcTicks)
        {
            if (eventCount <= 0 || !_exportScratch.IsCreated || !_snapshotBuffer.IsCreated)
                return false;

            int eventSizeBytes = UnsafeUtility.SizeOf<TelemetryEvent>();
            WriteHeader(eventCount, eventSizeBytes);

            unsafe
            {
                byte* exportBytesPtr = (byte*)_exportScratch.GetUnsafePtr();
                byte* payloadPtr = exportBytesPtr + BinaryHeaderSizeBytes;
                int copyBytes = eventCount * eventSizeBytes;
                int destinationBytes = _exportScratch.Length - BinaryHeaderSizeBytes;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(
                        payloadPtr,
                        destinationBytes,
                        _snapshotBuffer.GetUnsafeReadOnlyPtr(),
                        copyBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(GlobalTelemetryBus));
                    return false;
                }
            }

            _pendingGeneratedUtcTicks = generatedUtcTicks;
            _pendingEventCount = eventCount;
            _pendingByteCount = BinaryHeaderSizeBytes + (eventCount * eventSizeBytes);
            return true;
        }

        private static void WriteHeader(int eventCount, int eventSizeBytes)
        {
            unsafe
            {
                byte* exportBytesPtr = (byte*)_exportScratch.GetUnsafePtr();
                uint* header = (uint*)exportBytesPtr;
                header[0] = BinaryMagic;
                header[1] = Version;
                header[2] = (uint)eventCount;
                header[3] = (uint)eventSizeBytes;
            }
        }

        private static bool SignalExportThread()
        {
            if (!StartExportThread())
                return false;

            AutoResetEvent exportSignal = Volatile.Read(ref _exportSignal);
            if (exportSignal == null)
                return false;

            try
            {
                exportSignal.Set();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private static bool StartExportThread()
        {
            lock (_exportThreadGate)
            {
                if (_exportThread != null)
                {
                    if (_exportThread.IsAlive)
                        return true;

                    _exportSignal?.Dispose();
                    _exportSignal = null;
                    _exportThread = null;
                }

                try
                {
                    Volatile.Write(ref _exportStopRequested, 0);
                    // COLD ALLOC: AutoResetEvent[1] - persistent telemetry export wake signal - owner: GlobalTelemetryBus
                    _exportSignal = new AutoResetEvent(false);
                    // COLD ALLOC: Thread[1] - dedicated .h8dump writer - owner: GlobalTelemetryBus
                    _exportThread = new Thread(RunExportThread)
                    {
                        IsBackground = true,
                        Name = ExportThreadName,
                        Priority = System.Threading.ThreadPriority.BelowNormal
                    };
                    _exportThread.Start();
                    return true;
                }
                catch (Exception)
                {
                    _exportSignal?.Dispose();
                    _exportSignal = null;
                    _exportThread = null;
                    return false;
                }
            }
        }

        private static void StopExportThread()
        {
            Thread exportThread;
            AutoResetEvent exportSignal;
            lock (_exportThreadGate)
            {
                exportThread = _exportThread;
                exportSignal = _exportSignal;
                if (exportThread == null)
                {
                    exportSignal?.Dispose();
                    _exportSignal = null;
                    Volatile.Write(ref _exportStopRequested, 0);
                    return;
                }

                Volatile.Write(ref _exportStopRequested, 1);
                exportSignal?.Set();
            }

            if (!ReferenceEquals(Thread.CurrentThread, exportThread))
                exportThread.Join(ExportWorkerJoinMilliseconds);

            lock (_exportThreadGate)
            {
                if (ReferenceEquals(_exportThread, exportThread))
                    _exportThread = null;

                if (ReferenceEquals(_exportSignal, exportSignal))
                    _exportSignal = null;

                exportSignal?.Dispose();
                Volatile.Write(ref _exportStopRequested, 0);
            }
        }

        private static void RunExportThread()
        {
            while (true)
            {
                AutoResetEvent exportSignal = Volatile.Read(ref _exportSignal);
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

                DrainPendingExportRequest();

                if (Volatile.Read(ref _exportStopRequested) != 0)
                    return;
            }
        }

        private static void DrainPendingExportRequest()
        {
            int request = Volatile.Read(ref _pendingExportRequest);
            if (request == ExportRequestNone)
                return;

            try
            {
                if (request == ExportRequestEmergency)
                    TryEmergencyFlushLocked();
                else if (request == ExportRequestPrepared)
                    WritePreparedExportToMmf();
            }
            finally
            {
                if (request == ExportRequestEmergency)
                {
                    _snapshotInProgress = false;
                    _snapshotStartIndex = 0;
                    _snapshotTotalCount = 0;
                    _snapshotCopiedCount = 0;
                }

                ClearPendingExportState();
                Volatile.Write(ref _pendingExportRequest, ExportRequestNone);
                Interlocked.Exchange(ref _exportInFlight, 0);
            }
        }

        private static bool WritePreparedExportToMmf()
        {
            if (Interlocked.CompareExchange(ref _mmfWriteInProgress, 1, 0) != 0)
                return false;

            try
            {
                string telemetryDirectory = _pendingTelemetryDirectory;
                if (string.IsNullOrEmpty(telemetryDirectory))
                    return false;
                if (!_exportScratch.IsCreated)
                    return false;

                int pendingByteCount = _pendingByteCount;
                int pendingEventCount = _pendingEventCount;
                if (pendingEventCount <= 0 ||
                    pendingByteCount <= BinaryHeaderSizeBytes ||
                    pendingByteCount > _exportScratch.Length)
                {
                    return false;
                }

                Directory.CreateDirectory(telemetryDirectory);
                DateTime generatedUtc = _pendingGeneratedUtcTicks > 0L
                    ? new DateTime(_pendingGeneratedUtcTicks, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                _pendingBinaryPath = BuildTelemetryBinaryPath(telemetryDirectory, generatedUtc);

                if (!string.IsNullOrEmpty(_pendingBinaryPath))
                {
                    using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                               _pendingBinaryPath,
                               FileMode.Create,
                               null,
                               pendingByteCount,
                               MemoryMappedFileAccess.ReadWrite))
                    {
                        using (MemoryMappedViewAccessor mappedView = mappedFile.CreateViewAccessor(
                                   0L,
                                   pendingByteCount,
                                   MemoryMappedFileAccess.Write))
                        {
                            unsafe
                            {
                                byte* destination = null;
                                mappedView.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);
                                try
                                {
                                    if (!UnsafeMemoryCopyGuard.TryMemCpy(
                                            destination,
                                            pendingByteCount,
                                            _exportScratch.GetUnsafeReadOnlyPtr(),
                                            pendingByteCount))
                                    {
                                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(GlobalTelemetryBus));
                                        return false;
                                    }
                                }
                                finally
                                {
                                    mappedView.SafeMemoryMappedViewHandle.ReleasePointer();
                                }
                            }

                            mappedView.Flush();
                        }
                    }
                }

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
            catch (Exception)
            {
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _mmfWriteInProgress, 0);
            }
        }

        private static void ClearPendingExportState(bool clearDirectory = false)
        {
            _pendingEventCount = 0;
            _pendingByteCount = 0;
            _pendingBinaryPath = null;
            if (clearDirectory)
                _pendingTelemetryDirectory = null;
            _pendingGeneratedUtcTicks = 0L;
        }

        private static string BuildTelemetryBinaryPath(string telemetryDirectory, DateTime generatedUtc)
        {
            int directoryLength = telemetryDirectory.Length;
            bool needsSeparator =
                directoryLength > 0 &&
                telemetryDirectory[directoryLength - 1] != Path.DirectorySeparatorChar &&
                telemetryDirectory[directoryLength - 1] != Path.AltDirectorySeparatorChar;
            int pathLength =
                directoryLength +
                (needsSeparator ? 1 : 0) +
                TelemetryFileNamePrefix.Length +
                ExportTimestampCharCount +
                BinaryExtension.Length;

            return string.Create(
                pathLength,
                (Directory: telemetryDirectory, Timestamp: generatedUtc, NeedsSeparator: needsSeparator),
                static (span, state) =>
                {
                    int cursor = 0;
                    state.Directory.AsSpan().CopyTo(span);
                    cursor += state.Directory.Length;
                    if (state.NeedsSeparator)
                        span[cursor++] = Path.DirectorySeparatorChar;

                    TelemetryFileNamePrefix.AsSpan().CopyTo(span.Slice(cursor));
                    cursor += TelemetryFileNamePrefix.Length;

                    Span<char> timestampSpan = span.Slice(cursor, ExportTimestampCharCount);
                    if (!state.Timestamp.TryFormat(timestampSpan, out int _, ExportTimestampFormat.AsSpan(), CultureInfo.InvariantCulture))
                        timestampSpan.Fill('0');
                    cursor += ExportTimestampCharCount;

                    BinaryExtension.AsSpan().CopyTo(span.Slice(cursor));
                });
        }
    }
}
