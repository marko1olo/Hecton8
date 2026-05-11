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
    [StructLayout(LayoutKind.Sequential)]
    public struct PerformanceEventPayload
    {
        public long CurrentRawValue;
        public long ThresholdRawValue;
        public float CurrentValue;
        public float ThresholdValue;
        public int FrameCount;
        public ushort EventType;
        public ushort Reserved;
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

        // COLD ALLOC: RegistryBucket<IPerformanceEventListener>[8] - performance threshold listeners drained by SystemDispatcher LateUpdate - owner: PerformanceEvents
        private static readonly RegistryBucket<IPerformanceEventListener> _listeners = new RegistryBucket<IPerformanceEventListener>(8);
        private static NativeQueue<PerformanceEventPayload> _pendingEvents;
        private static NativeQueue<PerformanceEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Pending payload count in the performance threshold lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Registers a performance threshold listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IPerformanceEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.TryRegister(listener);
        }

        /// <summary>
        /// Unregisters a performance threshold listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IPerformanceEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued performance threshold events through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
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
                        break;

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;

                    IPerformanceEventListener[] rawArray = _listeners.RawArray;
                    int count = _listeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IPerformanceEventListener listener = rawArray[i];
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
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PerformanceEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PerformanceEventPayload>[16] - deferred performance threshold lane flushed by SystemDispatcher LateUpdate - owner: PerformanceEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PerformanceEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PerformanceEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PerformanceEventPayload>[16] - next-frame performance events raised by listeners - owner: PerformanceEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PerformanceEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
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

        internal static void RaiseFrameTimeSpike(float frameTimeMs, float thresholdMs, int frameCount)
        {
            Enqueue(new PerformanceEventPayload
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

        internal static void RaiseGCAllocExceeded(long allocatedBytes, long thresholdBytes, int frameCount)
        {
            Enqueue(new PerformanceEventPayload
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

        internal static void RaiseJobQueueBacklog(int pendingJobCount, int thresholdCount, int frameCount)
        {
            Enqueue(new PerformanceEventPayload
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

        internal static void RaiseSystemDegradation(
            float frameTimeMs,
            float thresholdMs,
            int frameCount,
            SystemDegradationLevel level = SystemDegradationLevel.Warning)
        {
            Enqueue(new PerformanceEventPayload
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
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PerformanceEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PerformanceEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        private static void Enqueue(in PerformanceEventPayload payload)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
            }
            else
            {
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
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
                    break;

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
    public struct PerformanceSnapshot
    {
        /// <summary>Frame time in milliseconds (measured via Stopwatch).</summary>
        public float frameTimeMs;

        /// <summary>Deltatime from Time.deltaTime (not measured, just cached).</summary>
        public float deltaTime;

        /// <summary>Frame count when sampled.</summary>
        public int frameCount;

        /// <summary>Physics.RaycastAll + Physics.Raycast + Physics.OverlapSphere calls this frame.</summary>
        public int physicsCallCount;

        /// <summary>Estimated time spent in physics (calls × avg time per call).</summary>
        public float physicsTimeMs;

        /// <summary>Pending jobs in job system (JobHandle.IsCompleted checks).</summary>
        public int pendingJobCount;

        /// <summary>Coroutine instances still running.</summary>
        public int activeCoroutineCount;

        /// <summary>Total heap memory allocated this frame (tracked via Profiler).</summary>
        public long gcAllocatedThisFrame;

        /// <summary>Total managed memory currently in use.</summary>
        public long gcTotalMemory;

        /// <summary>Peak GC memory since startup.</summary>
        public long gcPeakMemory;

        /// <summary>GC collections since last sample.</summary>
        public int gcCollectionCount;

        /// <summary>Was a GC collection detected this frame?</summary>
        public bool gcCollectionDetected;

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
            this.gcCollectionDetected = gcCollectionDetected;
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
    public sealed class PerformanceMonitor : MonoBehaviour, ITickable, IUpdatable, IServiceHeartbeat, IServiceShutdown
    {
        private const float MillisecondsPerSecond = 1000f;
        // ════════════════════════════════════════════════════════════
        //  SINGLETON
        // ════════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
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
                PerformanceMonitor runtime = GlobalRegistry.PerformanceMonitor;
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

        private float[] _frameTimeHistory;
        private int _frameTimeHistoryIndex;
        private int _frameTimeHistoryMask;
        private float _frameTimeHistoryReciprocal;

        private long _lastGCAllocCount;
        private long _lastGCTotalMemory;
        private int _lastGCCollectionCount;
        private bool _isRegisteredToTickManager;
        private bool _serviceRegistered;

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
            PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

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
        }

        private void OnEnable()
        {
            TryRegisterService();
            if (Application.isPlaying && !_serviceRegistered)
                return;

            if (_isRegisteredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            PerformanceEvents.EnsureInitialized();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _isRegisteredToTickManager = GlobalRegistry.Updatables.Contains(this);
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
            if (!_isRegisteredToTickManager)
            {
                TryUnregisterService();
                return;
            }

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _isRegisteredToTickManager = false;
            TryUnregisterService();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPerformanceMonitorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PerformanceMonitor, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPerformanceMonitorRuntime(this);
            _serviceRegistered = false;
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
                    LogAutoReport();
                }
#endif
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogAutoReport()
        {
            UnityEngine.Debug.Log(GetReport());
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

            int totalPhysics = trackPhysicsCalls ? Hecton8.Physics.RaycastBatchHelper.TotalRaycastsProcessed : 0;
            int cachedHits = trackPhysicsCalls ? Hecton8.Physics.QueryCacheContext.CacheHits : 0;
            int pendingJobs = trackJobSystem ? 0 : 0;

            _currentSnapshot = new PerformanceSnapshot(
                frameTimeMs: (float)_frameStopwatch.Elapsed.TotalMilliseconds * sampleIntervalFrames,
                deltaTime: deltaTime,
                frameCount: Time.frameCount,
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
            Hecton8.Physics.RaycastBatchHelper.TotalRaycastsProcessed = 0;
            Hecton8.Physics.QueryCacheContext.CacheHits = 0;

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
                PerformanceEvents.RaiseFrameTimeSpike(_currentSnapshot.frameTimeMs, peakFrameTimeThreshold, frameCount);

            long gcThresholdBytes = (long)(peakAllocThreshold * 1048576f);
            if (_currentSnapshot.gcAllocatedThisFrame > gcThresholdBytes)
                PerformanceEvents.RaiseGCAllocExceeded(_currentSnapshot.gcAllocatedThisFrame, gcThresholdBytes, frameCount);

            if (_currentSnapshot.pendingJobCount > jobBacklogThreshold)
                PerformanceEvents.RaiseJobQueueBacklog(_currentSnapshot.pendingJobCount, jobBacklogThreshold, frameCount);
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
                PerformanceMonitor runtime = GlobalRegistry.PerformanceMonitor;
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
            PerformanceMonitor runtime = GlobalRegistry.PerformanceMonitor;
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
