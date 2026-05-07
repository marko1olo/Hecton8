using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
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
        CatastrophicCascadePrevented = 15
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TelemetryEvent
    {
        public uint FrameIndex;
        public uint EventType;
        public uint SubjectHash;
        public uint ContextHash;
        public float ScalarValue;
        public float3 WorldPosition;
    }

    public static class GlobalTelemetryBus
    {
        private const int Capacity = 1024;
        private const int BinaryHeaderSizeBytes = 16;
        private const int Version = 1;
        private const uint BinaryMagic = 0x4D4C4554u; // "TELM"
        private const float DrainIntervalSeconds = 60f;
        private const int SnapshotCopyBudgetPerLateFrame = 128;
        private const string ExportFolderName = "Telemetry";
        private const string BinaryExtension = ".tbin";
        private const string JsonExtension = ".json";

        private static NativeArray<TelemetryEvent> _ringBuffer;
        private static NativeArray<TelemetryEvent> _snapshotBuffer;
        private static byte[] _exportBytes;
        private static int _writeCursor;
        private static float _nextDrainTimeSeconds = DrainIntervalSeconds;
        private static int _exportInFlight;
        private static int _mmfWriteInProgress;
        private static int _pendingEventCount;
        private static int _pendingByteCount;
        private static string _pendingBinaryPath;
        private static string _pendingJsonPath;
        private static string _pendingTelemetryDirectory;
        private static long _pendingGeneratedUtcTicks;
        private static bool _snapshotInProgress;
        private static int _snapshotStartIndex;
        private static int _snapshotTotalCount;
        private static int _snapshotCopiedCount;
        private static long _nativeCopyByteCount;
        private static int _nativeCopyOperationCount;

        private static class ManagedExportCallbacks
        {
            // COLD ALLOC: WaitCallback[1] - managed background telemetry export entry point - owner: GlobalTelemetryBus
            internal static readonly WaitCallback BackgroundExportCallback = ExecuteBackgroundExport;
        }

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
            bool workerOwnsExportState =
                Volatile.Read(ref _exportInFlight) != 0 ||
                Volatile.Read(ref _mmfWriteInProgress) != 0;
            JobHandle noDependency = default;
            DisposeNativeArray(ref _ringBuffer, noDependency);
            DisposeNativeArray(ref _snapshotBuffer, noDependency);

            _writeCursor = 0;
            _nextDrainTimeSeconds = DrainIntervalSeconds;
            _snapshotInProgress = false;
            _snapshotStartIndex = 0;
            _snapshotTotalCount = 0;
            _snapshotCopiedCount = 0;

            if (!workerOwnsExportState)
            {
                _exportBytes = null;
                _exportInFlight = 0;
                _mmfWriteInProgress = 0;
                ClearPendingExportState();
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
            float scalar = math.max(0f, trackedHeapBytes / (1024f * 1024f));
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
        /// Attempts a crash-path synchronous export of the global telemetry ring.
        /// Returns false when a background MMF write already owns the export scratch state.
        /// </summary>
        public static bool TryEmergencyFlushSynchronous()
        {
            if (!Application.isPlaying)
                return false;

            EnsureInitialized();
            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return false;

            try
            {
                int writeCursor = Volatile.Read(ref _writeCursor);
                int totalWritten = math.min(writeCursor, Capacity);
                if (totalWritten <= 0)
                    return false;

                CopySnapshotUnbounded(writeCursor - totalWritten, totalWritten);
                PrepareExportState(totalWritten, DateTime.UtcNow.Ticks);
                return WritePreparedExportToMmf();
            }
            finally
            {
                _snapshotInProgress = false;
                _snapshotStartIndex = 0;
                _snapshotTotalCount = 0;
                _snapshotCopiedCount = 0;
                ClearPendingExportState();
                Interlocked.Exchange(ref _exportInFlight, 0);
            }
        }

        private static void Publish(
            TelemetryEventType eventType,
            uint subjectHash,
            uint contextHash,
            float scalarValue,
            Vector3 worldPosition)
        {
            if (!Application.isPlaying)
                return;

            EnsureInitialized();

            int writeIndex = Interlocked.Increment(ref _writeCursor) - 1;
            int slot = writeIndex % Capacity;
            if (slot < 0)
                slot += Capacity;

            _ringBuffer[slot] = new TelemetryEvent
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                EventType = (uint)eventType,
                SubjectHash = subjectHash,
                ContextHash = contextHash,
                ScalarValue = scalarValue,
                WorldPosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z)
            };
        }

        private static void EnsureInitialized()
        {
            if (_ringBuffer.IsCreated)
                return;

            _ringBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] - global telemetry ring buffer - owner: GlobalTelemetryBus
            _snapshotBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] - telemetry export snapshot staging buffer - owner: GlobalTelemetryBus
            NativeMemorySentinel.RegisterNativeArray(
                _ringBuffer,
                nameof(GlobalTelemetryBus),
                nameof(_ringBuffer),
                NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(
                _snapshotBuffer,
                nameof(GlobalTelemetryBus),
                nameof(_snapshotBuffer),
                NativeAllocationLifetime.Session);
            _exportBytes = new byte[(Capacity * UnsafeUtility.SizeOf<TelemetryEvent>()) + BinaryHeaderSizeBytes]; // COLD ALLOC: byte[] telemetry export scratch - owner: GlobalTelemetryBus
        }

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

            int writeCursor = Volatile.Read(ref _writeCursor);
            int totalWritten = math.min(writeCursor, Capacity);
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
            if (!_snapshotInProgress || !_ringBuffer.IsCreated || !_snapshotBuffer.IsCreated)
                return;

            int remaining = _snapshotTotalCount - _snapshotCopiedCount;
            int copyCount = math.min(remaining, SnapshotCopyBudgetPerLateFrame);
            for (int i = 0; i < copyCount; i++)
            {
                int ringIndex = (_snapshotStartIndex + _snapshotCopiedCount + i) % Capacity;
                if (ringIndex < 0)
                    ringIndex += Capacity;

                _snapshotBuffer[_snapshotCopiedCount + i] = _ringBuffer[ringIndex];
            }

            _snapshotCopiedCount += copyCount;
            if (_snapshotCopiedCount < _snapshotTotalCount)
                return;

            CompleteSnapshotCopy();
        }

        private static void CopySnapshotUnbounded(int startIndex, int totalCount)
        {
            for (int i = 0; i < totalCount; i++)
            {
                int ringIndex = (startIndex + i) % Capacity;
                if (ringIndex < 0)
                    ringIndex += Capacity;

                _snapshotBuffer[i] = _ringBuffer[ringIndex];
            }
        }

        private static void CompleteSnapshotCopy()
        {
            PrepareExportState(_snapshotTotalCount, DateTime.UtcNow.Ticks);
            _snapshotInProgress = false;
            _snapshotStartIndex = 0;
            _snapshotTotalCount = 0;
            _snapshotCopiedCount = 0;
            ThreadPool.QueueUserWorkItem(ManagedExportCallbacks.BackgroundExportCallback);
        }

        private static void PrepareExportState(int eventCount, long generatedUtcTicks)
        {
            int eventSizeBytes = UnsafeUtility.SizeOf<TelemetryEvent>();
            WriteHeader(eventCount, eventSizeBytes);

            unsafe
            {
                fixed (byte* exportBytesPtr = _exportBytes)
                {
                    byte* payloadPtr = exportBytesPtr + BinaryHeaderSizeBytes;
                    int copyBytes = eventCount * eventSizeBytes;
                    int destinationBytes = _exportBytes.Length - BinaryHeaderSizeBytes;
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(
                            payloadPtr,
                            destinationBytes,
                            _snapshotBuffer.GetUnsafeReadOnlyPtr(),
                            copyBytes))
                    {
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(GlobalTelemetryBus));
                    }
                }
            }

            _pendingTelemetryDirectory = Path.Combine(Application.persistentDataPath, ExportFolderName);
            _pendingGeneratedUtcTicks = generatedUtcTicks;
            _pendingEventCount = eventCount;
            _pendingByteCount = BinaryHeaderSizeBytes + (eventCount * eventSizeBytes);
        }

        private static void WriteHeader(int eventCount, int eventSizeBytes)
        {
            unsafe
            {
                fixed (byte* exportBytesPtr = _exportBytes)
                {
                    uint* header = (uint*)exportBytesPtr;
                    header[0] = BinaryMagic;
                    header[1] = Version;
                    header[2] = (uint)eventCount;
                    header[3] = (uint)eventSizeBytes;
                }
            }
        }

        private static void ExecuteBackgroundExport(object state)
        {
            try
            {
                WritePreparedExportToMmf();
            }
            finally
            {
                ClearPendingExportState();
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

                Directory.CreateDirectory(telemetryDirectory);
                DateTime generatedUtc = _pendingGeneratedUtcTicks > 0L
                    ? new DateTime(_pendingGeneratedUtcTicks, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                string timestamp = generatedUtc.ToString("yyyyMMdd_HHmmss");
                _pendingBinaryPath = Path.Combine(telemetryDirectory, $"telemetry_{timestamp}{BinaryExtension}");
                _pendingJsonPath = Path.Combine(telemetryDirectory, $"telemetry_{timestamp}{JsonExtension}");

                if (_pendingByteCount > 0 && !string.IsNullOrEmpty(_pendingBinaryPath))
                {
                    using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                               _pendingBinaryPath,
                               FileMode.Create,
                               null,
                               _pendingByteCount,
                               MemoryMappedFileAccess.ReadWrite))
                    {
                        using (MemoryMappedViewStream mappedStream = mappedFile.CreateViewStream(
                                   0L,
                                   _pendingByteCount,
                                   MemoryMappedFileAccess.Write))
                        {
                            mappedStream.Write(_exportBytes, 0, _pendingByteCount);
                            mappedStream.Flush();
                        }
                    }
                }

                if (_pendingEventCount > 0 && !string.IsNullOrEmpty(_pendingJsonPath))
                {
                    StringBuilder builder = new StringBuilder(192);
                    builder.Append("{\"version\":");
                    builder.Append(Version);
                    builder.Append(",\"eventCount\":");
                    builder.Append(_pendingEventCount);
                    builder.Append(",\"binaryFile\":\"");
                    builder.Append(Path.GetFileName(_pendingBinaryPath));
                    builder.Append("\",\"generatedUtc\":\"");
                    builder.Append(DateTime.UtcNow.ToString("O"));
                    builder.Append("\"}");
                    File.WriteAllText(_pendingJsonPath, builder.ToString(), Encoding.UTF8);
                }

                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _mmfWriteInProgress, 0);
            }
        }

        private static void ClearPendingExportState()
        {
            _pendingEventCount = 0;
            _pendingByteCount = 0;
            _pendingBinaryPath = null;
            _pendingJsonPath = null;
            _pendingTelemetryDirectory = null;
            _pendingGeneratedUtcTicks = 0L;
        }
    }
}
