using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Hecton.Localization;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    internal static class GlobalTelemetryLayout
    {
        internal const int TelemetryEventStrideBytes = 64;
    }

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
        MemoryBreach = 25,
        DominantAxisTelemetry = 26,
        UnityLogFault = 27,
        InputSchemeHash = 28,
        PrologueStage = 29
    }

    [StructLayout(LayoutKind.Explicit, Size = GlobalTelemetryLayout.TelemetryEventStrideBytes)]
    public struct TelemetryEvent
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint EventType;
        [FieldOffset(8)] public uint SubjectHash;
        [FieldOffset(12)] public uint ContextHash;
        [FieldOffset(16)] public float ScalarValue;
        [FieldOffset(20)] public float3 WorldPosition;
        [FieldOffset(32)] public uint Reserved0;
        [FieldOffset(36)] public uint Reserved1;
        [FieldOffset(40)] public uint Reserved2;
        [FieldOffset(44)] public uint Reserved3;
        [FieldOffset(48)] public uint Reserved4;
        [FieldOffset(52)] public uint Reserved5;
        [FieldOffset(56)] public uint Reserved6;
        [FieldOffset(60)] public uint Reserved7;
    }

    public static partial class GlobalTelemetryBus
    {
        private const int RetainedFrameCount = 1000;
        private const int Capacity = 1024;
        private const int BinaryHeaderSizeBytes = 16;
        private const int Version = 1;
        private const uint BinaryMagic = 0x4D4C4554u; // "TELM"
        private const uint InputLagWarningHash = 0x494C4147u; // "ILAG"
        private const uint DominantAxisApproximationHash = 0x44415849u; // "DAXI"
        private const uint DistanceSquaredHash = 0x44535121u; // "DSQ!"
        private const float DrainIntervalSeconds = 60f;
        private const int SnapshotCopyBudgetPerLateFrame = 128;
        private const int ExportRequestNone = 0;
        private const int ExportRequestPrepared = 1;
        private const int ExportRequestEmergency = 2;
        private const SystemID NativeMemoryOwner = SystemID.CoreDiagnostics;
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
        private static long _pendingGeneratedUtcTicks;
        private static bool _snapshotInProgress;
        private static long _snapshotStartIndex;
        private static int _snapshotTotalCount;
        private static int _snapshotCopiedCount;
        private static long _nativeCopyByteCount;
        private static int _nativeCopyOperationCount;
        private static int _pendingExportRequest;
        // COLD ALLOC: object[1] — first-use native telemetry buffer initialization gate — owner: GlobalTelemetryBus
        private static readonly object _initGate = new object();

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
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                change == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                DisposeStaticState();
            }
        }
#endif

        internal static void DisposeStaticState()
        {
            H8Memory.UnregisterBeforeShutdownOwnerRelease(DisposeStaticState);
            DisposeBlackboxState();
            lock (_initGate)
            {
                JobHandle noDependency = default;
                DisposeRingBuffer(ref _ringBuffer);
                DisposeNativeArray(ref _snapshotBuffer, noDependency);
                DisposeNativeArray(ref _exportScratch, noDependency);

                _writeCursor = 0;
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _nextDrainTimeSeconds = DrainIntervalSeconds;
                _snapshotInProgress = false;
                _snapshotStartIndex = 0;
                _snapshotTotalCount = 0;
                _snapshotCopiedCount = 0;
                _exportInFlight = 0;
                _mmfWriteInProgress = 0;
                _pendingExportRequest = ExportRequestNone;
                ClearPendingExportState();
            }

            Interlocked.Exchange(ref _nativeCopyByteCount, 0L);
            Interlocked.Exchange(ref _nativeCopyOperationCount, 0);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            JobHandle disposeHandle = H8Memory.Release(ref array, dependency, NativeMemoryOwner);
            ForceCompleteDisposeHandleInPostSimulationWindow(ref disposeHandle);
        }

        private static void ForceCompleteDisposeHandleInPostSimulationWindow(ref JobHandle disposeHandle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static void DisposeRingBuffer(ref NativeRingBuffer<TelemetryEvent> buffer)
        {
            if (!buffer.IsCreated)
                return;

            buffer.UnregisterBackingArray();
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

        [Obsolete("Use PublishBiomeVisited(uint,int,float). Telemetry ingress must receive precomputed hashes.", true)]
        public static void PublishBiomeVisited(string biomeId, int depthTier, float depthMeters)
        {
            PublishBiomeVisited(ComputeContextHash(biomeId), depthTier, depthMeters);
        }

        public static void PublishBiomeVisited(uint biomeHash, int depthTier, float depthMeters)
        {
            Publish(
                TelemetryEventType.BiomeVisited,
                biomeHash,
                unchecked((uint)depthTier),
                depthMeters,
                default);
        }

        [Obsolete("Use PublishItemCrafted(uint). Telemetry ingress must receive precomputed hashes.", true)]
        public static void PublishItemCrafted(string itemPersistentId)
        {
            PublishItemCrafted(ComputeContextHash(itemPersistentId));
        }

        public static void PublishItemCrafted(uint itemHashId)
        {
            Publish(TelemetryEventType.ItemCrafted, itemHashId, 0u, 1f, default);
        }

        [Obsolete("Use PublishBootstrapDependencyCycle(uint,uint). Telemetry ingress must receive precomputed hashes.", true)]
        public static void PublishBootstrapDependencyCycle(string serviceId, string dependencyId)
        {
            PublishBootstrapDependencyCycle(ComputeContextHash(serviceId), ComputeContextHash(dependencyId));
        }

        public static void PublishBootstrapDependencyCycle(uint serviceHash, uint dependencyHash)
        {
            Publish(
                TelemetryEventType.BootstrapDependencyCycle,
                serviceHash,
                dependencyHash,
                0f,
                default);
        }

        [Obsolete("Use PublishJobBarrierStall(uint,uint,float). Telemetry ingress must receive precomputed hashes.", true)]
        public static void PublishJobBarrierStall(string systemName, string phaseName, float stallMilliseconds)
        {
            PublishJobBarrierStall(ComputeContextHash(systemName), ComputeContextHash(phaseName), stallMilliseconds);
        }

        public static void PublishJobBarrierStall(uint systemHash, uint phaseHash, float stallMilliseconds)
        {
            Publish(
                TelemetryEventType.JobBarrierStall,
                systemHash,
                phaseHash,
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
        /// Publishes the active input scheme hash into the fixed telemetry ring.
        /// </summary>
        public static void PublishInputSchemeHash(uint schemeHash, uint flags, uint frame)
        {
            Publish(TelemetryEventType.InputSchemeHash, schemeHash, flags, frame, default);
        }

        /// <summary>
        /// Publishes prologue sequence state transitions as hash-only stage telemetry.
        /// </summary>
        public static void PublishPrologueStage(uint stageHash, uint stateHash, uint flags)
        {
            Publish(TelemetryEventType.PrologueStage, stageHash, stateHash, flags, default);
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
        /// Publishes a threaded Unity log fault as hashed numeric payloads only.
        /// </summary>
        public static void PublishUnityLogFault(uint conditionHash, uint stackTraceHash, uint faultFlags)
        {
            Publish(TelemetryEventType.UnityLogFault, conditionHash, stackTraceHash, faultFlags, default);
        }

        /// <summary>
        /// Broadcasts a memory subsystem breach as numeric HUD/black-box payload only.
        /// </summary>
        public static void PublishMemoryBreachEvent(uint contextHash, float currentMegabytes)
        {
            Publish(TelemetryEventType.MemoryBreach, contextHash, 0u, math.max(0f, currentMegabytes), default);
            RequestBlackboxEmergencyDumpAsync(contextHash == 0u ? BlackboxEmergencyFlushHash : contextHash);
        }

        /// <summary>
        /// Publishes bot distance telemetry as numeric payloads only.
        /// </summary>
        public static void PublishDominantAxisTelemetry(uint botHash, float distanceOrMagnitudeSq, bool usedDominantAxisApproximation)
        {
            float scalarValue = math.isfinite(distanceOrMagnitudeSq)
                ? math.max(0f, distanceOrMagnitudeSq)
                : 0f;
            Publish(
                TelemetryEventType.DominantAxisTelemetry,
                botHash,
                usedDominantAxisApproximation ? DominantAxisApproximationHash : DistanceSquaredHash,
                scalarValue,
                default);
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

            CommitBlackboxFrame();

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
            EnsureInitialized();
            return TryWriteBlackboxDumpSynchronous(BlackboxEmergencyFlushHash);
        }

        /// <summary>
        /// Queues a raw full-ring crash dump from a non-main-thread log callback.
        /// </summary>
        public static void RequestEmergencyFlushAsync()
        {
            RequestBlackboxEmergencyDumpAsync(BlackboxEmergencyFlushHash);

            if (!_ringBuffer.IsCreated ||
                !_snapshotBuffer.IsCreated ||
                !_exportScratch.IsCreated)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return;

            Volatile.Write(ref _pendingExportRequest, ExportRequestEmergency);
            DrainPendingExportRequest();
        }

        /// <summary>
        /// Computes a deterministic FNV-1a hash for managed diagnostic text before it enters binary telemetry.
        /// Raw text is never written to `.h8dump`.
        /// </summary>
        /// <param name="value">Diagnostic text to hash.</param>
        /// <returns>FNV-1a hash, or zero for null/empty/whitespace payloads.</returns>
        public static uint ComputeContextHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0u;

            return ComputeFnv1A(value.AsSpan());
        }

        /// <summary>
        /// Attempts a background-thread raw full-ring dump without touching Unity path APIs.
        /// </summary>
        public static bool TryEmergencyFlushFromBackground()
        {
#if UNITY_EDITOR
            return false;
#else
            if (TryWriteBlackboxDumpFromBackground(BlackboxEmergencyFlushHash))
                return true;

            if (!_ringBuffer.IsCreated ||
                !_snapshotBuffer.IsCreated ||
                !_exportScratch.IsCreated)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return false;

            Volatile.Write(ref _pendingExportRequest, ExportRequestEmergency);
            DrainPendingExportRequest();
            return true;
#endif
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

            if (!CopySnapshotUnbounded(writeCursor - totalWritten, totalWritten))
                return false;

            if (!PrepareExportState(totalWritten, DateTime.UtcNow.Ticks))
                return false;

            return CommitPreparedExportInMemory();
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
                FrameIndex = isMainThread ? Hecton8.Core.SystemDispatcher.CurrentFrameId : 0u,
                EventType = (uint)eventType,
                SubjectHash = subjectHash,
                ContextHash = contextHash,
                ScalarValue = scalarValue,
                WorldPosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z)
            };

            _ringBuffer.Write(in telemetryEvent);
            Volatile.Write(ref _writeCursor, _ringBuffer.TotalWrites);
            PushEvent(subjectHash != 0u ? subjectHash : (uint)eventType, scalarValue, contextHash);
        }

        private static void EnsureInitialized()
        {
            if (_ringBuffer.IsCreated &&
                _snapshotBuffer.IsCreated &&
                _exportScratch.IsCreated)
            {
                return;
            }

            lock (_initGate)
            {
                H8Memory.RegisterBeforeShutdownOwnerRelease(DisposeStaticState);
                if (!_ringBuffer.IsCreated)
                {
                    _ringBuffer = new NativeRingBuffer<TelemetryEvent>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<TelemetryEvent>[1024] — power-of-two black-box ring retaining the last 1000 telemetry frames — owner: GlobalTelemetryBus
                    _ringBuffer.RegisterBackingArray(
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
                    _snapshotBuffer = H8Memory.Allocate<TelemetryEvent>(Capacity, NativeMemoryOwner, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }

                if (!_exportScratch.IsCreated)
                {
                    int exportScratchBytes = (Capacity * UnsafeUtility.SizeOf<TelemetryEvent>()) + BinaryHeaderSizeBytes;
                    _exportScratch = H8Memory.Allocate<byte>(exportScratchBytes, NativeMemoryOwner, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }

                EnsureBlackboxInitialized();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeHash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
        }

        private static uint ComputeFnv1A(ReadOnlySpan<char> value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
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
            if (!_ringBuffer.TryCopyRange(_snapshotStartIndex + _snapshotCopiedCount, copyCount, _snapshotBuffer, _snapshotCopiedCount))
            {
                AbortPendingSnapshotCopy();
                return;
            }

            _snapshotCopiedCount += copyCount;
            if (_snapshotCopiedCount < _snapshotTotalCount)
                return;

            CompleteSnapshotCopy();
        }

        private static bool CopySnapshotUnbounded(long startIndex, int totalCount)
        {
            return _ringBuffer.TryCopyRange(startIndex, totalCount, _snapshotBuffer);
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
                DrainPendingExportRequest();
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
                    CommitPreparedExportInMemory();
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

        private static bool CommitPreparedExportInMemory()
        {
            if (Interlocked.CompareExchange(ref _mmfWriteInProgress, 1, 0) != 0)
                return false;

            try
            {
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

                return true;
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

        private static void ClearPendingExportState()
        {
            _pendingEventCount = 0;
            _pendingByteCount = 0;
            _pendingGeneratedUtcTicks = 0L;
        }
    }
}
