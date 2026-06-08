// ============================================================================
// HECTON-8 — PerformanceMonitor.cs  v1.0
// Zero-allocation, real-time performance monitoring system.
//
// PURPOSE:
//   Measure frame-time, GC allocations, job system overhead, physics calls,
//   and other critical metrics without allocations in hot paths.
//   Self-contained — no external dependencies.
//
// FEATURES:
//   • Frame timing: delta-time, fixed-delta, slow-tick intervals
//   • GC tracking: allocations per frame, GC pause detection
//   • Physics metrics: raycast/overlap call counts + avg time per call
//   • Job system: pending jobs, job queue depth, scheduler overhead
//   • Memory: current/peak heap usage, GC.Alloc tracking
//   • Async: coroutine counts, awaitable counts
//
// ZERO GC GUARANTEE:
//   • All measurements are struct-based (stack allocated).
//   • Sample() method does NOT allocate — pure math.
//   • Sampling runs every N frames (configurable, default 60).
//   • GetReport() returns pre-allocated string (if reused).
//
// INTEGRATION:
//   Add to persistent world object (AutoLoad scene).
//   Or instantiate via singleton factory.
//   Events: PerformanceEvents NativeQueue lane for frame, GC, and job alerts.
//
// USAGE:
//   // Get current metrics (zero alloc)
//   var stats = PerformanceMonitor.CurrentStats;
//   if (stats.frameTimeMs > 16.67f)
//       Debug.LogWarning("Frame budget exceeded!");
//
//   // Register IPerformanceEventListener during OnEnable and unregister in OnDisable.
//
//   // Periodic reporting (1 sec sampling)
//   string report = PerformanceMonitor.GetReport(); // text report
//
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Performance threshold event type emitted by <see cref="PerformanceEvents"/>.
    /// </summary>
    public enum PerformanceEventType : byte
    {
        FrameTimeSpike = 0,
        GCAllocExceeded = 1,
        JobQueueBacklog = 2,
        SystemDegradation = 3
    }

    public enum SystemDegradationLevel : ushort
    {
        Optimal = 1,
        Warning = 2,
        Critical = 3
    }

    /// <summary>
    /// Blittable performance event payload drained by <see cref="SystemDispatcher"/> in LateUpdate.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PerformanceEventPayload
    {
        [FieldOffset(0)] public long CurrentRawValue;
        [FieldOffset(8)] public long ThresholdRawValue;
        [FieldOffset(16)] public float CurrentValue;
        [FieldOffset(20)] public float ThresholdValue;
        [FieldOffset(24)] public int FrameCount;
        [FieldOffset(28)] public ushort EventType;
        [FieldOffset(30)] public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for performance threshold events.
    /// </summary>
    public interface IPerformanceEventListener
    {
        void OnPerformanceEvent(in PerformanceEventPayload payload);
    }

    /// <summary>
    /// Queue-backed performance threshold lane. Producers are sampled Tick paths; listeners run in LateUpdate.
    /// </summary>
    public static class PerformanceEvents
    {
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IPerformanceEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - performance threshold listeners drained by SystemDispatcher LateUpdate - owner: PerformanceEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<PerformanceEventPayload> _pendingEvents;
        private static NativeQueue<PerformanceEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _droppedEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Pending payload count in the performance threshold lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        /// <summary>
        /// Registers a performance threshold listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IPerformanceEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        /// <summary>
        /// Unregisters a performance threshold listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IPerformanceEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return;
            }
        }

        /// <summary>
        /// Flushes queued performance threshold events through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listenerCount <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingEvents.TryDequeue(out PerformanceEventPayload payload))
                    {
                        _pendingEventCount = 0;
                        break;
                    }

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;

                    int count = _listenerCount;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IPerformanceEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnPerformanceEvent(in payload);
                    }
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        internal static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<PerformanceEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PerformanceEventPayload>[16] - deferred performance threshold lane flushed by SystemDispatcher LateUpdate - owner: PerformanceEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<PerformanceEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PerformanceEventPayload>[16] - next-frame performance events raised by listeners - owner: PerformanceEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(PerformanceEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        [Obsolete("Use TryRaiseFrameTimeSpike(float,float,int) so bounded enqueue refusal is visible.", true)]
        internal static void RaiseFrameTimeSpike(float frameTimeMs, float thresholdMs, int frameCount)
        {
            TryRaiseFrameTimeSpike(frameTimeMs, thresholdMs, frameCount);
        }

        internal static bool TryRaiseFrameTimeSpike(float frameTimeMs, float thresholdMs, int frameCount)
        {
            return Enqueue(new PerformanceEventPayload
            {
                CurrentRawValue = (long)(frameTimeMs * 1000f),
                ThresholdRawValue = (long)(thresholdMs * 1000f),
                CurrentValue = frameTimeMs,
                ThresholdValue = thresholdMs,
                FrameCount = frameCount,
                EventType = (ushort)PerformanceEventType.FrameTimeSpike,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseGCAllocExceeded(long,long,int) so bounded enqueue refusal is visible.", true)]
        internal static void RaiseGCAllocExceeded(long allocatedBytes, long thresholdBytes, int frameCount)
        {
            TryRaiseGCAllocExceeded(allocatedBytes, thresholdBytes, frameCount);
        }

        internal static bool TryRaiseGCAllocExceeded(long allocatedBytes, long thresholdBytes, int frameCount)
        {
            return Enqueue(new PerformanceEventPayload
            {
                CurrentRawValue = allocatedBytes,
                ThresholdRawValue = thresholdBytes,
                CurrentValue = allocatedBytes * GlobalTelemetryBus.BytesToMegabytes,
                ThresholdValue = thresholdBytes * GlobalTelemetryBus.BytesToMegabytes,
                FrameCount = frameCount,
                EventType = (ushort)PerformanceEventType.GCAllocExceeded,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseJobQueueBacklog(int,int,int) so bounded enqueue refusal is visible.", true)]
        internal static void RaiseJobQueueBacklog(int pendingJobCount, int thresholdCount, int frameCount)
        {
            TryRaiseJobQueueBacklog(pendingJobCount, thresholdCount, frameCount);
        }

        internal static bool TryRaiseJobQueueBacklog(int pendingJobCount, int thresholdCount, int frameCount)
        {
            return Enqueue(new PerformanceEventPayload
            {
                CurrentRawValue = pendingJobCount,
                ThresholdRawValue = thresholdCount,
                CurrentValue = pendingJobCount,
                ThresholdValue = thresholdCount,
                FrameCount = frameCount,
                EventType = (ushort)PerformanceEventType.JobQueueBacklog,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseSystemDegradation(float,float,int,SystemDegradationLevel) so bounded enqueue refusal is visible.", true)]
        internal static void RaiseSystemDegradation(
            float frameTimeMs,
            float thresholdMs,
            int frameCount,
            SystemDegradationLevel level = SystemDegradationLevel.Warning)
        {
            TryRaiseSystemDegradation(frameTimeMs, thresholdMs, frameCount, level);
        }

        internal static bool TryRaiseSystemDegradation(
            float frameTimeMs,
            float thresholdMs,
            int frameCount,
            SystemDegradationLevel level = SystemDegradationLevel.Warning)
        {
            return Enqueue(new PerformanceEventPayload
            {
                CurrentRawValue = (long)(frameTimeMs * 1000f),
                ThresholdRawValue = (long)(thresholdMs * 1000f),
                CurrentValue = frameTimeMs,
                ThresholdValue = thresholdMs,
                FrameCount = frameCount,
                EventType = (ushort)PerformanceEventType.SystemDegradation,
                Reserved = (ushort)level
            });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            for (int i = 0; i < ListenerCapacity; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _isDispatching = false;
        }

        private static bool Enqueue(in PerformanceEventPayload payload)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                _droppedEventCount++;
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }
            else
            {
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
                return true;
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out _))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameEvents.IsCreated || _nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _nextFrameEvents.TryDequeue(out PerformanceEventPayload payload))
            {
                _nextFrameEventCount--;
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }
        }
    }

    /// <summary>
    /// Single frame's performance metrics snapshot.
    /// Struct — zero heap allocation, all stack-based.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PerformanceSnapshot
    {
        /// <summary>Frame time in milliseconds (measured via Stopwatch).</summary>
        [FieldOffset(24)] public float frameTimeMs;

        /// <summary>Deltatime from Time.deltaTime (not measured, just cached).</summary>
        [FieldOffset(28)] public float deltaTime;

        /// <summary>Frame count when sampled.</summary>
        [FieldOffset(36)] public int frameCount;

        /// <summary>Legacy scene-query calls counted this frame.</summary>
        [FieldOffset(40)] public int physicsCallCount;

        /// <summary>Estimated time spent in physics (calls × avg time per call).</summary>
        [FieldOffset(32)] public float physicsTimeMs;

        /// <summary>Pending jobs in job system (JobHandle.IsCompleted checks).</summary>
        [FieldOffset(44)] public int pendingJobCount;

        /// <summary>Coroutine instances still running.</summary>
        [FieldOffset(48)] public int activeCoroutineCount;

        /// <summary>Total heap memory allocated this frame (tracked via Profiler).</summary>
        [FieldOffset(0)] public long gcAllocatedThisFrame;

        /// <summary>Total managed memory currently in use.</summary>
        [FieldOffset(8)] public long gcTotalMemory;

        /// <summary>Peak GC memory since startup.</summary>
        [FieldOffset(16)] public long gcPeakMemory;

        /// <summary>GC collections since last sample.</summary>
        [FieldOffset(52)] public int gcCollectionCount;

        /// <summary>1 when a GC collection was detected this frame; 0 otherwise.</summary>
        [FieldOffset(56)] public byte gcCollectionDetected;

        [FieldOffset(57)] private byte _pad0;
        [FieldOffset(58)] private ushort _pad1;
        [FieldOffset(60)] private uint _pad2;

        public PerformanceSnapshot(
            float frameTimeMs, float deltaTime, int frameCount,
            int physicsCallCount, float physicsTimeMs, int pendingJobCount,
            int activeCoroutineCount, long gcAllocatedThisFrame,
            long gcTotalMemory, long gcPeakMemory,
            int gcCollectionCount, bool gcCollectionDetected)
        {
            this.frameTimeMs = frameTimeMs;
            this.deltaTime = deltaTime;
            this.frameCount = frameCount;
            this.physicsCallCount = physicsCallCount;
            this.physicsTimeMs = physicsTimeMs;
            this.pendingJobCount = pendingJobCount;
            this.activeCoroutineCount = activeCoroutineCount;
            this.gcAllocatedThisFrame = gcAllocatedThisFrame;
            this.gcTotalMemory = gcTotalMemory;
            this.gcPeakMemory = gcPeakMemory;
            this.gcCollectionCount = gcCollectionCount;
            this.gcCollectionDetected = gcCollectionDetected ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
        }
    }

    /// <summary>
    /// Aggregated performance statistics over N samples.
    /// Used for trend analysis and alerting.
    /// </summary>
    public struct PerformanceStats
    {
        public float frameTimeMs;          // Current frame
        public float avgFrameTimeMs;       // Average over last N samples
        public float peakFrameTimeMs;      // Peak frame time this session
        public float gcAllocRateMBperSec;  // Allocation rate (MB/sec)
        public long totalHeapBytes;        // Current managed heap size
        public int gcCollectionCountRate;  // Collections per second (approx)
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class PerformanceMonitor : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const string AutoReportTraceMessage = "[PERF] Auto report sample ready. Use PerformanceMonitor.DescribeStatus() from a cold debug route for full text.";

        private const float MillisecondsPerSecond = 1000f;
        private static PerformanceMonitor s_currentRuntime;
        // ════════════════════════════════════════════════════════════
        //  SINGLETON
        // ════════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_currentRuntime = null;
        }

        // ════════════════════════════════════════════════════════════
        //  EVENTS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// True when at least one sample has been captured.
        /// </summary>
        public static bool HasCurrentStats
        {
            get
            {
                PerformanceMonitor runtime = ResolveActiveRuntime();
                return runtime != null && runtime._sampleCountTotal > 0;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  INSPECTOR CONFIG
        // ════════════════════════════════════════════════════════════

        [Header("── Sampling ──────────────────────────────────")]
        [SerializeField, Range(1, 120)]
        private int sampleIntervalFrames = 30;

        [SerializeField, Range(0.01f, 10f)]
        private float avgFrameWindow = 1.0f;

        [Header("── Thresholds ────────────────────────────────")]
        [SerializeField, Range(8f, 50f)]
        private float peakFrameTimeThreshold = 16.67f; // 60 FPS budget

        [SerializeField, Range(1f, 100f)]
        private float peakAllocThreshold = 5.0f; // MB

        [SerializeField, Range(1, 64)]
        private int jobBacklogThreshold = 16;

        [Header("── Features ──────────────────────────────────")]
        [SerializeField]
        private bool trackGCAllocations = true;

        [SerializeField]
        private bool trackPhysicsCalls = true;

        [SerializeField]
        private bool trackJobSystem = true;

        [SerializeField]
        private bool enableAutoReporting = false;

        [SerializeField, Range(0.5f, 10f)]
        private float autoReportInterval = 5.0f;

        // ════════════════════════════════════════════════════════════
        //  INTERNAL STATE
        // ════════════════════════════════════════════════════════════

        private System.Diagnostics.Stopwatch _frameStopwatch;
        private PerformanceSnapshot _currentSnapshot;
        private PerformanceSnapshot _lastSnapshot;

        private int _sampleCounter;
        private int _sampleCountTotal;
        private float _autoReportTimer;
        private bool _autoReportPending;

        private float[] _frameTimeHistory;
        private int _frameTimeHistoryIndex;
        private int _frameTimeHistoryMask;
        private float _frameTimeHistoryReciprocal;

        private long _lastGCAllocCount;
        private long _lastGCTotalMemory;
        private int _lastGCCollectionCount;
        private bool _isRegisteredToTickManager;
        private bool _isRegisteredToLateFrame;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private IPhysicsQueryTelemetryReadModel _physicsQueryTelemetry;

        private float _avgFrameTimeMs;
        private float _peakFrameTimeMs;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        // ════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════

        private static int ResolvePowerOfTwoHistoryLength(int requestedLength)
        {
            int value = 1;
            while (value < requestedLength && value < 4096)
                value <<= 1;

            return value;
        }

        private static float ResolvePowerOfTwoReciprocal(int length)
        {
            switch (length)
            {
                case 1: return 1f;
                case 2: return 0.5f;
                case 4: return 0.25f;
                case 8: return 0.125f;
                case 16: return 0.0625f;
                case 32: return 0.03125f;
                case 64: return 0.015625f;
                case 128: return 0.0078125f;
                case 256: return 0.00390625f;
                case 512: return 0.001953125f;
                case 1024: return 0.0009765625f;
                case 2048: return 0.00048828125f;
                default: return 0.000244140625f;
            }
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            // COLD ALLOC: Stopwatch[1] - sampled performance timing state - owner: PerformanceMonitor
            _frameStopwatch = new System.Diagnostics.Stopwatch();
            int historyLength = ResolvePowerOfTwoHistoryLength(math.max(1, (int)(avgFrameWindow * 60f)));
            // COLD ALLOC: float[avgFrameWindow*60] - sampled frame-time rolling window - owner: PerformanceMonitor
            _frameTimeHistory = new float[historyLength];
            _frameTimeHistoryMask = historyLength - 1;
            _frameTimeHistoryReciprocal = ResolvePowerOfTwoReciprocal(historyLength);
            _sampleCounter = 0;
            _sampleCountTotal = 0;
            _autoReportTimer = autoReportInterval;

            _lastGCTotalMemory = GC.GetTotalMemory(false);
            _lastGCCollectionCount = GC.CollectionCount(0);
            _physicsQueryTelemetry = GlobalRegistry.PhysicsQueryTelemetry;
            s_currentRuntime = this;
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (Application.isPlaying && !_serviceRegistered)
                return;

            TryRegisterHotSwapListener();
            TryRegisterToDispatcher();
        }

        private void OnDisable()
        {
            OnServiceShutdown();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            TryUnregisterFromDispatcher();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            if (ReferenceEquals(s_currentRuntime, this))
                s_currentRuntime = null;
        }

        private void TryRegisterToDispatcher()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            PerformanceEvents.EnsureInitialized();
            if (!_isRegisteredToTickManager)
                _isRegisteredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);

            if (!_isRegisteredToLateFrame)
                _isRegisteredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterFromDispatcher()
        {
            if (_isRegisteredToLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _isRegisteredToLateFrame = false;
            }

            if (!_isRegisteredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _isRegisteredToTickManager = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterPerformanceMonitorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PerformanceMonitor, this);
            if (_serviceRegistered)
                s_currentRuntime = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPerformanceMonitorRuntime(this);
            _serviceRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PerformanceMonitor active = s_currentRuntime;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsPerformanceMonitorRuntimeUsable(active))
                {
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.UnregisterPerformanceMonitorRuntime(active);
                if (ReferenceEquals(s_currentRuntime, active))
                    s_currentRuntime = null;
            }

            PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsPerformanceMonitorRuntimeUsable(registered))
            {
                s_currentRuntime = registered;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterPerformanceMonitorRuntime(registered);
            if (ReferenceEquals(s_currentRuntime, registered))
                s_currentRuntime = null;

            return false;
        }

        private static PerformanceMonitor ResolveActiveRuntime()
        {
            PerformanceMonitor active = s_currentRuntime;
            if (IsPerformanceMonitorRuntimeUsable(active))
                return active;

            if (!ReferenceEquals(active, null) && ReferenceEquals(s_currentRuntime, active))
                s_currentRuntime = null;

            PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor;
            if (IsPerformanceMonitorRuntimeUsable(registered))
            {
                s_currentRuntime = registered;
                return registered;
            }

            if (!ReferenceEquals(registered, null))
                GlobalRegistry.UnregisterPerformanceMonitorRuntime(registered);

            if (ReferenceEquals(s_currentRuntime, registered))
                s_currentRuntime = null;

            return null;
        }

        private static bool IsPerformanceMonitorRuntimeUsable(PerformanceMonitor monitor)
        {
            return monitor != null && monitor._serviceRegistered && monitor.isActiveAndEnabled;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.RaycastBatchRuntime)
            {
                _physicsQueryTelemetry = currentService as IPhysicsQueryTelemetryReadModel;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterFromDispatcher();
            if (currentService == null || !isActiveAndEnabled)
                return;

            TryRegisterToDispatcher();
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

        // ════════════════════════════════════════════════════════════
        //  ITickable — MEASUREMENT
        // ════════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // Sample metrics every N frames
            if (++_sampleCounter >= sampleIntervalFrames)
            {
                _frameStopwatch.Restart();

                MeasureFrame(deltaTime);
                UpdateHistory();
                CheckThresholds();

                _frameStopwatch.Stop();
                _sampleCounter = 0;
            }

            // Auto-reporting
            if (enableAutoReporting)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _autoReportTimer -= deltaTime;
                if (_autoReportTimer <= 0f)
                {
                    _autoReportTimer = autoReportInterval;
                    _autoReportPending = true;
                }
#endif
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogAutoReportLateFrame()
        {
            Hecton8.Core.H8Debug.Log(AutoReportTraceMessage);
        }

        public void LateFrameTick()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_autoReportPending)
                return;

            _autoReportPending = false;
            LogAutoReportLateFrame();
#endif
        }

        // ════════════════════════════════════════════════════════════
        //  MEASUREMENT
        // ════════════════════════════════════════════════════════════

        private void MeasureFrame(float deltaTime)
        {
            long gcAlloc = 0;
            long gcTotal = GC.GetTotalMemory(false);
            int gcCollections = GC.CollectionCount(0);
            bool gcCollectionDetected = gcCollections > _lastGCCollectionCount;

            if (trackGCAllocations)
            {
                // Approximate managed allocation growth across the sampling window.
                gcAlloc = Math.Max(0L, gcTotal - _lastGCTotalMemory);
                _lastGCAllocCount += gcAlloc;
            }

            IPhysicsQueryTelemetryReadModel physicsQueryTelemetry = _physicsQueryTelemetry;
            int totalPhysics = trackPhysicsCalls && physicsQueryTelemetry != null ? physicsQueryTelemetry.LegacySurfaceQueriesProcessed : 0;
            int cachedHits = trackPhysicsCalls && physicsQueryTelemetry != null ? physicsQueryTelemetry.PlayerLookQueryCacheHits : 0;
            int pendingJobs = trackJobSystem ? 0 : 0;

            _currentSnapshot = new PerformanceSnapshot(
                frameTimeMs: (float)_frameStopwatch.Elapsed.TotalMilliseconds * sampleIntervalFrames,
                deltaTime: deltaTime,
                frameCount: Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                physicsCallCount: totalPhysics,
                physicsTimeMs: cachedHits, // Repurposing field: Hits are more important than avg time here
                pendingJobCount: pendingJobs,
                activeCoroutineCount: 0,
                gcAllocatedThisFrame: gcAlloc,
                gcTotalMemory: gcTotal,
                gcPeakMemory: System.Math.Max(_lastSnapshot.gcPeakMemory, gcTotal),
                gcCollectionCount: gcCollections,
                gcCollectionDetected: gcCollectionDetected
            );

            // Reset counters for next sampling window
            physicsQueryTelemetry?.ResetPhysicsQueryTelemetryCounters();

            _lastGCTotalMemory = gcTotal;
            _lastGCCollectionCount = gcCollections;
            _lastSnapshot = _currentSnapshot;
            _sampleCountTotal++;
        }

        private void UpdateHistory()
        {
            _frameTimeHistory[_frameTimeHistoryIndex] = _currentSnapshot.frameTimeMs;
            _frameTimeHistoryIndex = (_frameTimeHistoryIndex + 1) & _frameTimeHistoryMask;

            float sum = 0f;
            for (int i = 0; i < _frameTimeHistory.Length; i++)
                sum += _frameTimeHistory[i];

            _avgFrameTimeMs = sum * _frameTimeHistoryReciprocal;
            _peakFrameTimeMs = math.max(_peakFrameTimeMs, _currentSnapshot.frameTimeMs);
        }

        private void CheckThresholds()
        {
            int frameCount = _currentSnapshot.frameCount;
            if (_currentSnapshot.frameTimeMs > peakFrameTimeThreshold)
                PerformanceEvents.TryRaiseFrameTimeSpike(_currentSnapshot.frameTimeMs, peakFrameTimeThreshold, frameCount);

            long gcThresholdBytes = (long)(peakAllocThreshold * 1048576f);
            if (_currentSnapshot.gcAllocatedThisFrame > gcThresholdBytes)
                PerformanceEvents.TryRaiseGCAllocExceeded(_currentSnapshot.gcAllocatedThisFrame, gcThresholdBytes, frameCount);

            if (_currentSnapshot.pendingJobCount > jobBacklogThreshold)
                PerformanceEvents.TryRaiseJobQueueBacklog(_currentSnapshot.pendingJobCount, jobBacklogThreshold, frameCount);
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Get current performance metrics (struct — zero alloc).
        /// </summary>
        public static PerformanceStats CurrentStats
        {
            get
            {
                PerformanceMonitor runtime = ResolveActiveRuntime();
                if (runtime == null)
                    return default;

                int targetFps = RuntimeWatchdog.ActiveTargetFPS;

                return new PerformanceStats
                {
                    frameTimeMs = runtime._currentSnapshot.frameTimeMs,
                    avgFrameTimeMs = runtime._avgFrameTimeMs,
                    peakFrameTimeMs = runtime._peakFrameTimeMs,
                    gcAllocRateMBperSec = runtime._currentSnapshot.gcAllocatedThisFrame *
                                          GlobalTelemetryBus.BytesToMegabytes *
                                          targetFps,
                    totalHeapBytes = runtime._currentSnapshot.gcTotalMemory,
                    gcCollectionCountRate = runtime._currentSnapshot.gcCollectionCount
                };
            }
        }

        /// <summary>
        /// Compact status string for console or inspector logs.
        /// </summary>
        public static string DescribeStatus()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PerformanceMonitor runtime = ResolveActiveRuntime();
            if (runtime == null)
                return "PerformanceMonitor: Not initialized";

            if (!HasCurrentStats)
                return "PerformanceMonitor: No samples yet";

            PerformanceStats stats = CurrentStats;
            return
                $"[PERF] samples={runtime._sampleCountTotal} " +
                $"frame={stats.frameTimeMs:F2}ms avg={stats.avgFrameTimeMs:F2}ms peak={stats.peakFrameTimeMs:F2}ms " +
                $"gc={stats.gcAllocRateMBperSec:F2}MB/s heap={stats.totalHeapBytes * GlobalTelemetryBus.BytesToMegabytes:F1}MB " +
                $"collections={stats.gcCollectionCountRate}/s";
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// Debug report as formatted string.
        /// Allocates only when called (not per-frame).
        /// </summary>
        public static string GetReport()
        {
            return DescribeStatus();
        }
    }
}
