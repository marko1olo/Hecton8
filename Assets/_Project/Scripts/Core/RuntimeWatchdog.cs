using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Core
{
    /// <summary>
    /// Runtime liveness monitor and deterministic load-shed enforcer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9490)]
    public sealed class RuntimeWatchdog : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        public interface IEmergencyResetTarget
        {
            void ServiceEmergencyReset();
        }

        public interface IEmergencyColdTickCullTarget
        {
            int ApplyEmergencyColdTickCull();
        }

        public enum RuntimeWatchdogLane : byte
        {
            DispatcherUpdate = 0,
            DispatcherLateFrame = 1,
            CrashTelemetry = 2,
            FaunaDirector = 3,
            WorldStreaming = 4,
            Worker0 = 16,
            Worker1 = 17,
            Worker2 = 18,
            Worker3 = 19,
            Worker4 = 20,
            Worker5 = 21,
            Worker6 = 22,
            Worker7 = 23,
        }

        public const int TargetFPS = 60;
        public const int VrTargetFPS = 72;

        private const int LaneCapacity = 32;
        private const int SampleIntervalFrames = 60;
        private const int FrameStripConsecutiveFrames = 180;
        private const int FaunaEmergencyCullCooldownFrames = 60;
        private const int FaunaEmergencyCullEmptyCooldownFrames = 15;
        private const double EmergencyResetFailureCooldownSeconds = 1.0d;
        private const double StallThresholdSeconds = 5.0;
        private const double FrozenServiceThresholdSeconds = 2.0;
        private const double RegistryHeartbeatGuardIntervalSeconds = 60.0d;
        private const float BaseFrameStripThresholdSeconds = 0.01667f;
        private const float XrFrameStripThresholdSeconds = 0.0075f;
        private const float BaseFaunaArteryBudgetMs = 2.0f;
        private const float BaseHudHeartbeatTimeoutSeconds = 0.2f;
        private const float GcSteadyStateWarmupSeconds = 5f;
        private const float MmfHealthCheckIntervalSeconds = 60f;
        private const float MmfHealthRetryDelaySeconds = 5f;
        private const float BytesToMegabytes = GlobalTelemetryBus.BytesToMegabytes;
        private const long BaseMmfBloatThresholdBytes = 50L * 1024L * 1024L;
        private const long RuntimeMemorySpikeThresholdBytes = 50L * 1024L * 1024L;
        private const long DefaultRuntimeMemorySafeBoundBytes = 3L * 1024L * 1024L * 1024L;
        private const int MemorySpikeSampleIntervalFrames = 12;
        private const int MemorySubsystemBreachCooldownFrames = 300;
        private const int InputLagAnalyzerCooldownFrames = 30;
        private const long InvalidMmfSectorHash = long.MinValue;
        private const int RegistryHeartbeatSlotCount = (int)GlobalRegistryServiceSlot.Unknown;
        private const uint WatchdogStateFrameBudgetWarningSent = 1u << 0;
        private const uint WatchdogDegradationActionMask = 1u << 30;

        private static readonly uint _watchdogContextHash = unchecked((uint)LocHash.Compute(nameof(RuntimeWatchdog)));
        private static readonly uint _budgetStripHash = unchecked((uint)LocHash.Compute("WATCHDOG_BUDGET_STRIP"));
        private static readonly uint _faunaEmergencyCullHash = unchecked((uint)LocHash.Compute("FAUNA_EMERGENCY_CULL"));
        private static readonly uint _mmfBloatAlarmHash = unchecked((uint)LocHash.Compute("MMF_BLOAT_ALARM"));
        private static readonly uint _uiDeadlockHash = unchecked((uint)LocHash.Compute("UI_DEADLOCK"));
        private static readonly uint _criticalGcSpikeHash = unchecked((uint)LocHash.Compute("CRITICAL_GC_SPIKE"));
        private static readonly uint _runtimeMemorySpikeHash = unchecked((uint)LocHash.Compute("RUNTIME_MEMORY_SPIKE"));
        private static readonly uint _memorySubsystemBreachHash = unchecked((uint)LocHash.Compute("MEMORY_SUBSYSTEM_BREACH"));
        private static readonly uint _registryHeartbeatStaleHash = unchecked((uint)LocHash.Compute("REGISTRY_HEARTBEAT_STALE"));
        private static readonly uint _fastTickSteadyStateHash = unchecked((uint)LocHash.Compute("FAST_TICK_STEADY_STATE"));
        private static readonly uint _nativeLeakReapedHash = unchecked((uint)LocHash.Compute("NATIVE_LEAK_REAPED"));
        private static readonly uint _nativeLeakLabelHash = unchecked((uint)LocHash.Compute("NATIVE_LEAK_LABEL"));
        private static readonly uint _nanSentinelRecoveryHash = unchecked((uint)LocHash.Compute("NAN_SENTINEL_RECOVERY"));
        private static readonly double _millisecondsToStopwatchTicks = Stopwatch.Frequency * 0.001d;
        private static readonly double _stopwatchTicksToSeconds = 1.0d / Stopwatch.Frequency;
        private static readonly long _baseFaunaArteryBudgetTicks = MillisecondsToStopwatchTicks(BaseFaunaArteryBudgetMs);
        private static readonly long _xrFaunaArteryBudgetTicks = Math.Max(1L, _baseFaunaArteryBudgetTicks >> 1);

        // COLD ALLOC: int[32] — cross-thread liveness counters — owner: RuntimeWatchdog
        private static readonly int[] _heartbeatCounters = new int[LaneCapacity];
        // COLD ALLOC: int[32] — sampled liveness counters — owner: RuntimeWatchdog
        private static readonly int[] _lastObservedCounters = new int[LaneCapacity];
        // COLD ALLOC: double[32] — last heartbeat change timestamps — owner: RuntimeWatchdog
        private static readonly double[] _lastChangeTimes = new double[LaneCapacity];
        // COLD ALLOC: bool[32] — active liveness lane mask — owner: RuntimeWatchdog
        private static readonly bool[] _activeLanes = new bool[LaneCapacity];
        // COLD ALLOC: object[32] - frozen-service recovery callback table, avoids interface arrays - owner: RuntimeWatchdog
        private static readonly object[] _emergencyResetTargets = new object[LaneCapacity];
        // COLD ALLOC: int[255] - registry service TickCount samples - owner: RuntimeWatchdog
        private static readonly int[] _registryHeartbeatTicks = new int[RegistryHeartbeatSlotCount];
        // COLD ALLOC: byte[255] - active registry heartbeat sample mask - owner: RuntimeWatchdog
        private static readonly byte[] _registryHeartbeatActive = new byte[RegistryHeartbeatSlotCount];
        // COLD ALLOC: object[255] - boot/hot-swap cached heartbeat services, avoids interface arrays - owner: RuntimeWatchdog
        private static readonly object[] _registryHeartbeatServices = new object[RegistryHeartbeatSlotCount];

        // COLD ALLOC: object[1] — MMF background size result synchronization — owner: RuntimeWatchdog
        private static readonly object _mmfHealthResultLock = new object();
        // COLD ALLOC: WaitCallback[1] — MMF background size probe entry point — owner: RuntimeWatchdog
        private static readonly WaitCallback _mmfHealthCallback = ExecuteMmfHealthCheck;

        private static Canvas _hudCanvas;
        private static double _lastHudCanvasUpdateTime;
        private static int _hudDeadlockRecoveryFrame;
        private static int _nextFaunaEmergencyCullFrame;
        private static int _mmfHealthCheckInFlight;
        private static int _mmfHealthResultReady;
        private static int _mmfHealthGeneration;
        private static int _mmfHealthResultGeneration;
        private static long _mmfHealthResultBytes;
        private static long _mmfHealthResultSectorHash;
        private static string _mmfHealthWorkPath;
        private static long _mmfHealthWorkSectorHash;
        private static int _mmfHealthWorkGeneration;
        private static long _runtimeMemorySafeBoundBytes;
        private static long _runtimeMemoryBreachBoundBytes;

        private bool _registeredUpdatable;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwapListener;
        private IRuntimeWatchdogWorldHealthBridge _persistentWorldRegistry;
        private uint _watchdogStateFlags;
        private int _nextSampleFrame;
        private int _consecutiveOverBudgetFrames;
        private uint _lastInputLatencySequence;
        private int _lastConsumedMmfHealthGeneration;
        private int _lastGen0CollectionCount;
        private int _lastGcSpikeFrame = -1;
        private int _lastMemorySpikeFrame = -1;
        private int _nextMemorySpikeSampleFrame;
        private int _lastMemoryBreachFrame = -1;
        private int _lastInputClockSkewFrame = -1;
        private int _steadyStateGcGen0CollectionsDelta;
        private float _gcSteadyStateWarmupRemaining = GcSteadyStateWarmupSeconds;
        private bool _gcSteadyStateActive;
        private double _nextMmfHealthCheckTime;
        private double _nextRegistryHeartbeatGuardTime;
        private long _lastMmfBytes = -1L;
        private long _lastMmfSectorHash = InvalidMmfSectorHash;
        private long _lastTotalAllocatedMemoryBytes;
        private long _runtimeMemorySpikeThresholdBytes = RuntimeMemorySpikeThresholdBytes;
        private bool _runtimeOwnerRejected;

        public static int ActiveTargetFPS => HectonXRRuntimeState.IsXRActive ? VrTargetFPS : TargetFPS;
        public ServiceHeartbeatState HeartbeatState => _registeredUpdatable ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Booting;
        public bool IsServiceReady => _registeredUpdatable;
        public int SteadyStateGcGen0CollectionsDelta => _steadyStateGcGen0CollectionsDelta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Array.Clear(_heartbeatCounters, 0, _heartbeatCounters.Length);
            Array.Clear(_lastObservedCounters, 0, _lastObservedCounters.Length);
            Array.Clear(_lastChangeTimes, 0, _lastChangeTimes.Length);
            Array.Clear(_activeLanes, 0, _activeLanes.Length);
            Array.Clear(_emergencyResetTargets, 0, _emergencyResetTargets.Length);
            Array.Clear(_registryHeartbeatTicks, 0, _registryHeartbeatTicks.Length);
            Array.Clear(_registryHeartbeatActive, 0, _registryHeartbeatActive.Length);
            Array.Clear(_registryHeartbeatServices, 0, _registryHeartbeatServices.Length);
            _hudCanvas = null;
            _lastHudCanvasUpdateTime = 0d;
            _hudDeadlockRecoveryFrame = 0;
            _nextFaunaEmergencyCullFrame = 0;
            _mmfHealthCheckInFlight = 0;
            _mmfHealthResultReady = 0;
            _mmfHealthGeneration = 0;
            _mmfHealthResultGeneration = 0;
            _mmfHealthResultBytes = 0L;
            _mmfHealthResultSectorHash = InvalidMmfSectorHash;
            _mmfHealthWorkPath = null;
            _mmfHealthWorkSectorHash = InvalidMmfSectorHash;
            _mmfHealthWorkGeneration = 0;
            _runtimeMemorySafeBoundBytes = 0L;
            _runtimeMemoryBreachBoundBytes = 0L;
        }

        public static RuntimeWatchdog EnsureRuntimeInstance()
        {
            RuntimeWatchdog watchdog = GlobalRegistry.RuntimeWatchdog;
            if (watchdog != null)
                return watchdog;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Watchdog owns hang/stall detection and safe-halt telemetry; without create
            // the slot stays null when bootstrap reorders or skips EnsureRuntimeWatchdogRegistered.
            GameObject runtimeRoot = new GameObject("[RuntimeWatchdog]"); // COLD ALLOC: GameObject[1] - bootstrap-owned watchdog root - owner: RuntimeWatchdog
            watchdog = runtimeRoot.AddComponent<RuntimeWatchdog>();
            watchdog.InitializeService();
            return watchdog;
        }


        public static void Signal(RuntimeWatchdogLane lane)
        {
            int laneIndex = (int)lane;
            if ((uint)laneIndex >= LaneCapacity)
                return;

            _activeLanes[laneIndex] = true;
            Interlocked.Increment(ref _heartbeatCounters[laneIndex]);
        }

        /// <summary>
        /// Returns conservative memory headroom in bytes for streaming load gates.
        /// </summary>
        public static long GetAvailableMemory()
        {
            CacheRuntimeMemorySafeBoundBytes();
            long safeBoundBytes = Volatile.Read(ref _runtimeMemorySafeBoundBytes);
            long reservedBytes = Profiler.GetTotalReservedMemoryLong();
            return math.max(0L, safeBoundBytes - reservedBytes);
        }

        /// <summary>
        /// Reports a subsystem cost sample for the current frame without managed payloads.
        /// </summary>
        /// <param name="subsystemHash">Stable subsystem hash.</param>
        /// <param name="costMilliseconds">Measured cost in milliseconds.</param>
        public static void ReportSubsystemCost(uint subsystemHash, float costMilliseconds)
        {
            FrameTimeWatchdog.ReportSubsystemCost(subsystemHash, costMilliseconds);
        }

        internal static long BeginFaunaArterySample()
        {
            return Stopwatch.GetTimestamp();
        }

        internal static void EndFaunaArterySample(long startTimestamp)
        {
            if (startTimestamp <= 0L || !Application.isPlaying)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= 0L)
                return;

            if (elapsedTicks <= ResolveFaunaArteryBudgetTicks())
                return;

            float elapsedMilliseconds = ResolveFaunaArteryBudgetMilliseconds();
            ForceFaunaEmergencyColdTick(elapsedMilliseconds);
        }

        internal static void MarkHudCanvasUpdated(Canvas canvas)
        {
            if (!Application.isPlaying)
                return;

            if (canvas != null)
                _hudCanvas = canvas;
            _lastHudCanvasUpdateTime = ResolveWatchdogRealtimeSeconds();
        }

        internal static void RegisterEmergencyResetTarget(RuntimeWatchdogLane lane, IEmergencyResetTarget target)
        {
            int laneIndex = (int)lane;
            if ((uint)laneIndex >= LaneCapacity || target == null)
                return;

            _emergencyResetTargets[laneIndex] = target;
            _lastObservedCounters[laneIndex] = Volatile.Read(ref _heartbeatCounters[laneIndex]);
            _lastChangeTimes[laneIndex] = ResolveWatchdogRealtimeSeconds();
            _activeLanes[laneIndex] = true;
        }

        internal static void UnregisterEmergencyResetTarget(RuntimeWatchdogLane lane, IEmergencyResetTarget target)
        {
            int laneIndex = (int)lane;
            if ((uint)laneIndex >= LaneCapacity)
                return;

            if (!ReferenceEquals(_emergencyResetTargets[laneIndex], target))
                return;

            _emergencyResetTargets[laneIndex] = null;
            _activeLanes[laneIndex] = false;
            _lastChangeTimes[laneIndex] = 0d;
            _lastObservedCounters[laneIndex] = Volatile.Read(ref _heartbeatCounters[laneIndex]);
        }

        internal static void ReapNativeSceneLeaks(string context)
        {
            int reapedCount = NativeMemorySentinel.ReapSceneLifetimeLeaks(context);
            if (reapedCount <= 0)
                return;

            PublishPerformanceWarningNoThrow(
                _nativeLeakReapedHash,
                _watchdogContextHash,
                reapedCount);
        }

        internal static void ReportNativeLeakReaped(uint ownerHash, uint labelHash, long bytes)
        {
            float megabytes = bytes <= 0L ? 0f : math.min(float.MaxValue, bytes * BytesToMegabytes);
            PublishPerformanceWarningNoThrow(_nativeLeakReapedHash, ownerHash, megabytes);
            if (labelHash != 0u)
                PublishPerformanceWarningNoThrow(_nativeLeakLabelHash, labelHash, megabytes);
        }

        internal static Vector3 ReportRigidbodyNanRecovery(
            uint updatingSystemHash,
            Vector3 invalidRuntimePosition,
            Vector3 lastKnownGoodRuntimePosition)
        {
            Vector3 recoveredPosition = CrashTelemetryBuffer.ReportNanPhysicsRecovery(
                invalidRuntimePosition,
                lastKnownGoodRuntimePosition);
            PublishPerformanceWarningNoThrow(
                _nanSentinelRecoveryHash,
                updatingSystemHash,
                1f);
            return recoveredPosition;
        }

        public void InitializeService()
        {
            if (_runtimeOwnerRejected)
                return;

            GlobalTelemetryBus.Initialize();
            MathGuard.Initialize();
            FrameTimeWatchdog.InitializeCold();
            BlackBoxHeartbeatThread.Start();
            GlobalRegistry.RegisterRuntimeWatchdogRuntime(this);
            RefreshRegistryDependenciesCold();
            ResetGcCollectionSentinel();
            ResetMemorySpikeTracker();
            ResetRegistryHeartbeatGuard(ResolveWatchdogRealtimeSeconds());
            TryRegisterHotSwapListener();
            TryRegisterDispatcherLanes();
        }

        private void Awake()
        {
            if (!TryClaimRuntimeOwnership())
                return;

            _nextSampleFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + SampleIntervalFrames;
            double now = ResolveWatchdogRealtimeSeconds();
            _nextMmfHealthCheckTime = now + MmfHealthCheckIntervalSeconds;
            ResetRegistryHeartbeatGuard(now);
            GlobalTelemetryBus.Initialize();
            MathGuard.Initialize();
            FrameTimeWatchdog.InitializeCold();
            ResetGcCollectionSentinel();
            ResetMemorySpikeTracker();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerRejected)
                return;

            BlackBoxHeartbeatThread.Start();
            TryRegisterDispatcherLanes();
        }

        private void Start()
        {
            if (_runtimeOwnerRejected)
                return;

            TryRegisterDispatcherLanes();
        }

        private void OnDisable()
        {
            TryUnregisterDispatcherLanes();

            if (_registeredHotSwapListener)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwapListener = false;
            }

            BlackBoxHeartbeatThread.Stop();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void OnServiceShutdown()
        {
            OnDisable();
            if (ReferenceEquals(GlobalRegistry.RuntimeWatchdog, this))
                GlobalRegistry.UnregisterRuntimeWatchdogRuntime(this);
            FrameTimeWatchdog.Shutdown();
            MathGuard.Dispose();
            _watchdogStateFlags = 0u;
            _consecutiveOverBudgetFrames = 0;
            _lastInputLatencySequence = 0u;
            _lastConsumedMmfHealthGeneration = 0;
            _lastMmfBytes = -1L;
            _lastMmfSectorHash = InvalidMmfSectorHash;
            _nextRegistryHeartbeatGuardTime = 0d;
            _lastMemoryBreachFrame = -1;
            _lastInputClockSkewFrame = -1;
            _persistentWorldRegistry = null;
            ResetGcCollectionSentinel();
            ResetMemorySpikeTracker();
        }

        private bool TryClaimRuntimeOwnership()
        {
            RuntimeWatchdog registeredWatchdog = GlobalRegistry.RuntimeWatchdog;
            if (registeredWatchdog != null && !ReferenceEquals(registeredWatchdog, this))
            {
                if (Application.isPlaying)
                {
                    _runtimeOwnerRejected = true;
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }

                return false;
            }

            _runtimeOwnerRejected = false;
            if (Application.isPlaying && registeredWatchdog == null)
                GlobalRegistry.RegisterRuntimeWatchdogRuntime(this);

            return true;
        }

        public void Tick(float deltaTime)
        {
            BlackBoxHeartbeatThread.Ping();
            if (SimulationSignalRoute.SimulationPaused)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            ConsumeMmfHealthResult();
            FrameTimeWatchdog.Tick();
            EnforceFrameBudget(deltaTime);
            TickGcCollectionSentinel(deltaTime);

            double now = ResolveWatchdogRealtimeSeconds();
            EnforceHudHeartbeat(now, frame);
            QueueMmfHealthCheckIfDue(now);
            if (now >= _nextRegistryHeartbeatGuardTime)
                SampleRegistryHeartbeatsIfDue(now);
        }

        public void SlowTick()
        {
            BlackBoxHeartbeatThread.Ping();
            if (SimulationSignalRoute.SimulationPaused)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            TickMemorySpikeTracker(frame);
            if (frame < _nextSampleFrame)
                return;

            _nextSampleFrame = frame + SampleIntervalFrames;
            NativeMemorySentinel.AuditLongLivedTransientAllocations(frame);
            SampleRuntimeLanes(ResolveWatchdogRealtimeSeconds());
        }

        private void ResetGcCollectionSentinel()
        {
            _lastGen0CollectionCount = GC.CollectionCount(0);
            _lastGcSpikeFrame = -1;
            _steadyStateGcGen0CollectionsDelta = 0;
            _gcSteadyStateWarmupRemaining = GcSteadyStateWarmupSeconds;
            _gcSteadyStateActive = false;
        }

        private void TickGcCollectionSentinel(float deltaTime)
        {
            int currentGen0CollectionCount = GC.CollectionCount(0);
            if (!_gcSteadyStateActive)
            {
                if (deltaTime > 0f)
                    _gcSteadyStateWarmupRemaining -= deltaTime;

                _lastGen0CollectionCount = currentGen0CollectionCount;
                if (_gcSteadyStateWarmupRemaining > 0f)
                    return;

                _gcSteadyStateActive = true;
                return;
            }

            int delta = currentGen0CollectionCount - _lastGen0CollectionCount;
            if (delta <= 0)
            {
                if (delta < 0)
                    _lastGen0CollectionCount = currentGen0CollectionCount;
                return;
            }

            _lastGen0CollectionCount = currentGen0CollectionCount;
            _steadyStateGcGen0CollectionsDelta = _steadyStateGcGen0CollectionsDelta > int.MaxValue - delta
                ? int.MaxValue
                : _steadyStateGcGen0CollectionsDelta + delta;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastGcSpikeFrame == frame)
                return;

            _lastGcSpikeFrame = frame;
            PublishCriticalGcSpikeNoThrow(_criticalGcSpikeHash, _fastTickSteadyStateHash, delta);
        }

        private void ResetMemorySpikeTracker()
        {
            CacheRuntimeMemorySafeBoundBytes();
            _runtimeMemorySpikeThresholdBytes = ResolveScaledByteThreshold(RuntimeMemorySpikeThresholdBytes);
            _lastTotalAllocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            _lastMemorySpikeFrame = -1;
            _lastMemoryBreachFrame = -1;
            _nextMemorySpikeSampleFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + MemorySpikeSampleIntervalFrames;
        }

        private void TickMemorySpikeTracker(int frame)
        {
            if (frame < _nextMemorySpikeSampleFrame)
                return;

            _nextMemorySpikeSampleFrame = frame + MemorySpikeSampleIntervalFrames;
            long currentBytes = Profiler.GetTotalAllocatedMemoryLong();
            long previousBytes = _lastTotalAllocatedMemoryBytes;
            long breachBoundBytes = ResolveCachedRuntimeMemoryBreachBoundBytes();
            if (currentBytes >= breachBoundBytes)
            {
                long earlyDeltaBytes = currentBytes > previousBytes && previousBytes > 0L ? currentBytes - previousBytes : 0L;
                uint memoryContextHash = ResolveMemorySpikeFingerprint(previousBytes, currentBytes, earlyDeltaBytes, frame);
                TriggerMemorySubsystemBreachIfUnsafe(currentBytes, breachBoundBytes, frame, memoryContextHash);
            }

            if (previousBytes <= 0L)
            {
                _lastTotalAllocatedMemoryBytes = currentBytes;
                return;
            }

            long deltaBytes = currentBytes - previousBytes;
            _lastTotalAllocatedMemoryBytes = currentBytes;
            if (deltaBytes <= _runtimeMemorySpikeThresholdBytes)
                return;

            if (_lastMemorySpikeFrame == frame)
                return;

            _lastMemorySpikeFrame = frame;
            uint spikeHash = ResolveMemorySpikeFingerprint(previousBytes, currentBytes, deltaBytes, frame);
            GlobalTelemetryBus.RequestEmergencyFlushAsync();
            CrashTelemetryBuffer.ReportRuntimeMemorySpike(previousBytes, currentBytes, deltaBytes, spikeHash);
            float deltaMegabytes = deltaBytes * BytesToMegabytes;
            PublishPerformanceWarningNoThrow(_runtimeMemorySpikeHash, spikeHash, deltaMegabytes);
        }

        private void TriggerMemorySubsystemBreachIfUnsafe(long currentBytes, long breachBoundBytes, int frame, uint contextHash)
        {
            if (currentBytes < breachBoundBytes ||
                (_lastMemoryBreachFrame >= 0 && frame - _lastMemoryBreachFrame < MemorySubsystemBreachCooldownFrames))
            {
                return;
            }

            _lastMemoryBreachFrame = frame;
            GlobalTelemetryBus.PublishMemoryBreachEvent(contextHash, currentBytes * BytesToMegabytes);
            PublishPerformanceWarningNoThrow(
                _memorySubsystemBreachHash,
                contextHash,
                currentBytes * BytesToMegabytes);
        }

        private static void CacheRuntimeMemorySafeBoundBytes()
        {
            long cachedBoundBytes = Volatile.Read(ref _runtimeMemorySafeBoundBytes);
            if (cachedBoundBytes > 0L)
                return;

            long systemMemoryMegabytes = SystemInfo.systemMemorySize;
            if (systemMemoryMegabytes <= 0L)
            {
                Volatile.Write(ref _runtimeMemorySafeBoundBytes, DefaultRuntimeMemorySafeBoundBytes);
                Volatile.Write(ref _runtimeMemoryBreachBoundBytes, ComputeMemoryBreachBoundBytes(DefaultRuntimeMemorySafeBoundBytes));
                return;
            }

            long systemMemoryBytes = systemMemoryMegabytes * 1024L * 1024L;
            long safeBoundBytes = (systemMemoryBytes >> 1) + (systemMemoryBytes >> 2);
            safeBoundBytes = math.max(ResolveScaledByteThreshold(RuntimeMemorySpikeThresholdBytes), safeBoundBytes);
            Volatile.Write(ref _runtimeMemorySafeBoundBytes, safeBoundBytes);
            Volatile.Write(ref _runtimeMemoryBreachBoundBytes, ComputeMemoryBreachBoundBytes(safeBoundBytes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ResolveCachedRuntimeMemoryBreachBoundBytes()
        {
            long cachedBoundBytes = Volatile.Read(ref _runtimeMemoryBreachBoundBytes);
            return cachedBoundBytes > 0L
                ? cachedBoundBytes
                : ComputeMemoryBreachBoundBytes(DefaultRuntimeMemorySafeBoundBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ComputeMemoryBreachBoundBytes(long safeBoundBytes)
        {
            return (safeBoundBytes * 62259L) >> 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveMemorySpikeFingerprint(long previousBytes, long currentBytes, long deltaBytes, int frame)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = MixHash(hash, (uint)frame);
                hash = MixHash(hash, (uint)previousBytes);
                hash = MixHash(hash, (uint)(previousBytes >> 32));
                hash = MixHash(hash, (uint)currentBytes);
                hash = MixHash(hash, (uint)(currentBytes >> 32));
                hash = MixHash(hash, (uint)deltaBytes);
                return MixHash(hash, (uint)(deltaBytes >> 32));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixHash(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private void EnforceFrameBudget(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                _consecutiveOverBudgetFrames = 0;
                return;
            }

            if (deltaTime > ResolveFrameStripThresholdSeconds())
            {
                _consecutiveOverBudgetFrames++;
            }
            else
            {
                _consecutiveOverBudgetFrames = 0;
                return;
            }

            if ((_watchdogStateFlags & WatchdogStateFrameBudgetWarningSent) != 0u ||
                _consecutiveOverBudgetFrames < FrameStripConsecutiveFrames)
            {
                return;
            }

            _watchdogStateFlags |= WatchdogStateFrameBudgetWarningSent;
            PerformanceEvents.TryRaiseSystemDegradation(
                deltaTime * 1000f,
                ResolveFrameStripThresholdSeconds() * 1000f,
                Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
            GlobalTelemetryBus.PublishSystemDegradation(
                _budgetStripHash,
                WatchdogDegradationActionMask,
                deltaTime * 1000f);
            PublishPerformanceWarningNoThrow(
                _budgetStripHash,
                _watchdogContextHash,
                deltaTime * 1000f);
        }

        public void LateFrameTick()
        {
            Signal(RuntimeWatchdogLane.DispatcherLateFrame);
            BlackBoxHeartbeatThread.Ping();
            MathGuard.DrainInvalidNumberErrors();
            FrameTimeWatchdog.LateFrameTick();
            ReportInputClockSkewIfUnsafe(Hecton8.Core.SystemDispatcher.CurrentFrameIndex);

            uint latencySequence = InputLatencyTracker.CompletedSequence;
            if (latencySequence == _lastInputLatencySequence)
                return;

            _lastInputLatencySequence = latencySequence;
            float latencyMs = InputLatencyTracker.SampleCompletedLatencyMs();
            if (latencyMs <= AwaitableDebtMonitor.LatencyCrimeThreshold)
                return;

            GlobalTelemetryBus.PublishInputLagWarning(latencyMs);
            CrashTelemetryBuffer.ReportLatencyCrime(
                AwaitableDebtMonitor.PendingNextFrameContinuations,
                latencyMs);
        }

        private void ReportInputClockSkewIfUnsafe(int frame)
        {
            if (_lastInputClockSkewFrame >= 0 &&
                frame - _lastInputClockSkewFrame < InputLagAnalyzerCooldownFrames)
            {
                return;
            }

            float clockDeltaMs = InputLatencyTracker.SampleInputSystemClockDeltaMs();
            if (clockDeltaMs <= AwaitableDebtMonitor.LatencyCrimeThreshold)
                return;

            _lastInputClockSkewFrame = frame;
            GlobalTelemetryBus.PublishInputLagWarning(clockDeltaMs);
            CrashTelemetryBuffer.ReportLatencyCrime(
                AwaitableDebtMonitor.PendingNextFrameContinuations,
                clockDeltaMs);
        }

        private static void ForceFaunaEmergencyColdTick(float elapsedMilliseconds)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextFaunaEmergencyCullFrame)
                return;

            IEmergencyColdTickCullTarget target =
                _emergencyResetTargets[(int)RuntimeWatchdogLane.FaunaDirector] as IEmergencyColdTickCullTarget;
            if (target == null)
                return;

            int culledCount = target.ApplyEmergencyColdTickCull();
            if (culledCount <= 0)
            {
                _nextFaunaEmergencyCullFrame = frame + FaunaEmergencyCullEmptyCooldownFrames;
                return;
            }

            _nextFaunaEmergencyCullFrame = frame + FaunaEmergencyCullCooldownFrames;
            PublishPerformanceWarningNoThrow(
                _faunaEmergencyCullHash,
                _watchdogContextHash,
                elapsedMilliseconds);
        }

        private static float ResolveScaledThreshold(float baseValue)
        {
            return HectonXRRuntimeState.IsXRActive ? baseValue * 0.5f : baseValue;
        }

        private static double ResolveScaledThreshold(double baseValue)
        {
            return HectonXRRuntimeState.IsXRActive ? baseValue * 0.5d : baseValue;
        }

        private static float ResolveFrameStripThresholdSeconds()
        {
            return HectonXRRuntimeState.IsXRActive ? XrFrameStripThresholdSeconds : BaseFrameStripThresholdSeconds;
        }

        private static long ResolveFaunaArteryBudgetTicks()
        {
            return HectonXRRuntimeState.IsXRActive ? _xrFaunaArteryBudgetTicks : _baseFaunaArteryBudgetTicks;
        }

        private static float ResolveFaunaArteryBudgetMilliseconds()
        {
            return HectonXRRuntimeState.IsXRActive ? BaseFaunaArteryBudgetMs * 0.5f : BaseFaunaArteryBudgetMs;
        }

        private static long ResolveScaledByteThreshold(long baseValue)
        {
            return HectonXRRuntimeState.IsXRActive ? Math.Max(1L, baseValue >> 1) : baseValue;
        }

        private static long MillisecondsToStopwatchTicks(float milliseconds)
        {
            return Math.Max(1L, (long)(milliseconds * _millisecondsToStopwatchTicks));
        }

        private static double ResolveWatchdogRealtimeSeconds()
        {
            return Stopwatch.GetTimestamp() * _stopwatchTicksToSeconds;
        }

        private void EnforceHudHeartbeat(double now, int frame)
        {
            Canvas canvas = _hudCanvas;
            if (canvas == null)
            {
                _lastHudCanvasUpdateTime = now;
                return;
            }

            if (_lastHudCanvasUpdateTime <= 0d)
            {
                _lastHudCanvasUpdateTime = now;
                return;
            }

            double timeoutSeconds = ResolveScaledThreshold(BaseHudHeartbeatTimeoutSeconds);
            double elapsedSeconds = now - _lastHudCanvasUpdateTime;
            if (elapsedSeconds <= timeoutSeconds || _hudDeadlockRecoveryFrame == frame)
                return;

            TriggerHudCanvasBuildBatch(canvas);
            _hudDeadlockRecoveryFrame = frame;
            _lastHudCanvasUpdateTime = now;
            PublishPerformanceWarningNoThrow(
                _uiDeadlockHash,
                _watchdogContextHash,
                (float)(elapsedSeconds * 1000d));
        }

        private static void TriggerHudCanvasBuildBatch(Canvas canvas)
        {
            if (canvas == null || !canvas.enabled)
                return;

            // Canvas rebuild recovery is retired for the visor HUD path; the watchdog only reports the stale heartbeat.
        }

        private void ResetRegistryHeartbeatGuard(double now)
        {
            Array.Clear(_registryHeartbeatTicks, 0, _registryHeartbeatTicks.Length);
            Array.Clear(_registryHeartbeatActive, 0, _registryHeartbeatActive.Length);
            _nextRegistryHeartbeatGuardTime = now + RegistryHeartbeatGuardIntervalSeconds;
        }

        private void SampleRegistryHeartbeatsIfDue(double now)
        {
            if (now < _nextRegistryHeartbeatGuardTime)
                return;

            _nextRegistryHeartbeatGuardTime = now + RegistryHeartbeatGuardIntervalSeconds;
            for (int slot = 0; slot < RegistryHeartbeatSlotCount; slot++)
            {
                object service = _registryHeartbeatServices[slot];
                IServiceHeartbeat heartbeat = service as IServiceHeartbeat;
                ISystem system = service as ISystem;
                if (heartbeat == null && system == null)
                {
                    _registryHeartbeatActive[slot] = 0;
                    _registryHeartbeatTicks[slot] = 0;
                    continue;
                }

                if (heartbeat != null &&
                    (!heartbeat.IsServiceReady ||
                     heartbeat.HeartbeatState == ServiceHeartbeatState.Failed ||
                     heartbeat.HeartbeatState == ServiceHeartbeatState.Shutdown))
                {
                    _registryHeartbeatActive[slot] = 0;
                    _registryHeartbeatTicks[slot] = 0;
                    continue;
                }

                int tickCount = system != null ? system.TickCount : heartbeat.TickCount;
                if (_registryHeartbeatActive[slot] != 0 && _registryHeartbeatTicks[slot] == tickCount)
                {
                    GlobalTelemetryBus.PublishRegistryHeartbeatStale((uint)slot, unchecked((uint)tickCount));
                    PublishPerformanceWarningNoThrow(_registryHeartbeatStaleHash, (uint)slot, 1f);
                    CrashTelemetryBuffer.ReportRuntimeWatchdogStall((uint)slot, unchecked((uint)tickCount));
                }

                _registryHeartbeatTicks[slot] = tickCount;
                _registryHeartbeatActive[slot] = 1;
            }
        }

        private void QueueMmfHealthCheckIfDue(double now)
        {
            if (now < _nextMmfHealthCheckTime)
                return;

            IRuntimeWatchdogWorldHealthBridge registry = _persistentWorldRegistry;
            if (registry == null ||
                !registry.TryGetIndexedSaveHealth(out string savePath, out long sectorHash) ||
                string.IsNullOrEmpty(savePath))
            {
                _nextMmfHealthCheckTime = now + MmfHealthRetryDelaySeconds;
                return;
            }

            if (Interlocked.CompareExchange(ref _mmfHealthCheckInFlight, 1, 0) != 0)
            {
                _nextMmfHealthCheckTime = now + MmfHealthRetryDelaySeconds;
                return;
            }

            AssignMmfHealthWork(savePath, sectorHash, Interlocked.Increment(ref _mmfHealthGeneration));
            try
            {
                if (ThreadPool.UnsafeQueueUserWorkItem(_mmfHealthCallback, null))
                {
                    _nextMmfHealthCheckTime = now + MmfHealthCheckIntervalSeconds;
                }
                else
                {
                    ClearMmfHealthWork();
                    _nextMmfHealthCheckTime = now + MmfHealthRetryDelaySeconds;
                    Interlocked.Exchange(ref _mmfHealthCheckInFlight, 0);
                }
            }
            catch (Exception)
            {
                ClearMmfHealthWork();
                _nextMmfHealthCheckTime = now + MmfHealthRetryDelaySeconds;
                Interlocked.Exchange(ref _mmfHealthCheckInFlight, 0);
            }
        }

        private static void AssignMmfHealthWork(string path, long sectorHash, int generation)
        {
            Volatile.Write(ref _mmfHealthWorkSectorHash, sectorHash);
            Volatile.Write(ref _mmfHealthWorkPath, path);
            Volatile.Write(ref _mmfHealthWorkGeneration, generation);
        }

        private static void ClearMmfHealthWork()
        {
            Volatile.Write(ref _mmfHealthWorkGeneration, 0);
            Volatile.Write(ref _mmfHealthWorkPath, null);
            Volatile.Write(ref _mmfHealthWorkSectorHash, InvalidMmfSectorHash);
        }

        private static void ReadMmfHealthWork(out string path, out long sectorHash, out int generation)
        {
            generation = Volatile.Read(ref _mmfHealthWorkGeneration);
            path = Volatile.Read(ref _mmfHealthWorkPath);
            sectorHash = Volatile.Read(ref _mmfHealthWorkSectorHash);
        }

        private static void ExecuteMmfHealthCheck(object state)
        {
            long bytes = -1L;
            long sectorHash = InvalidMmfSectorHash;
            int generation = 0;
            try
            {
                ReadMmfHealthWork(out string path, out sectorHash, out generation);
                if (!string.IsNullOrEmpty(path) && TryGetFileLength(path, out long fileBytes))
                    bytes = fileBytes;
            }
            catch (UnauthorizedAccessException)
            {
                bytes = -1L;
            }
            catch (IOException)
            {
                bytes = -1L;
            }
            catch (Exception)
            {
                bytes = -1L;
            }
            finally
            {
                lock (_mmfHealthResultLock)
                {
                    _mmfHealthResultBytes = bytes;
                    _mmfHealthResultSectorHash = sectorHash;
                    _mmfHealthResultGeneration = generation;
                    Volatile.Write(ref _mmfHealthResultReady, 1);
                }

                ClearMmfHealthWork();
                Interlocked.Exchange(ref _mmfHealthCheckInFlight, 0);
            }
        }

        private static bool TryGetFileLength(string path, out long bytes)
        {
            bytes = -1L;
            if (string.IsNullOrEmpty(path))
                return false;

            FileInfo info = new FileInfo(path);
            if (!info.Exists)
                return false;

            bytes = info.Length;
            return true;
        }

        private void ConsumeMmfHealthResult()
        {
            if (Volatile.Read(ref _mmfHealthResultReady) == 0)
                return;

            long bytes;
            long sectorHash;
            int generation;
            lock (_mmfHealthResultLock)
            {
                bytes = _mmfHealthResultBytes;
                sectorHash = _mmfHealthResultSectorHash;
                generation = _mmfHealthResultGeneration;
                Volatile.Write(ref _mmfHealthResultReady, 0);
            }

            if (generation <= _lastConsumedMmfHealthGeneration || bytes < 0L)
                return;

            _lastConsumedMmfHealthGeneration = generation;
            if (sectorHash != _lastMmfSectorHash)
            {
                _lastMmfSectorHash = sectorHash;
                _lastMmfBytes = bytes;
                return;
            }

            if (_lastMmfBytes < 0L)
            {
                _lastMmfBytes = bytes;
                return;
            }

            long deltaBytes = bytes - _lastMmfBytes;
            if (deltaBytes > ResolveScaledByteThreshold(BaseMmfBloatThresholdBytes))
            {
                PublishPerformanceWarningNoThrow(
                    _mmfBloatAlarmHash,
                    _watchdogContextHash,
                    math.min(float.MaxValue, deltaBytes * BytesToMegabytes));
            }

            _lastMmfBytes = bytes;
        }

        private void SampleRuntimeLanes(double now)
        {
            for (int laneIndex = 0; laneIndex < LaneCapacity; laneIndex++)
            {
                if (!_activeLanes[laneIndex])
                    continue;

                int currentCounter = Volatile.Read(ref _heartbeatCounters[laneIndex]);
                if (currentCounter != _lastObservedCounters[laneIndex])
                {
                    _lastObservedCounters[laneIndex] = currentCounter;
                    _lastChangeTimes[laneIndex] = now;
                    continue;
                }

                double lastChange = _lastChangeTimes[laneIndex];
                if (lastChange <= 0d)
                {
                    _lastChangeTimes[laneIndex] = now;
                    continue;
                }

                double thresholdSeconds = ResolveScaledThreshold(IsEmergencyResetLane(laneIndex)
                    ? FrozenServiceThresholdSeconds
                    : StallThresholdSeconds);
                if (now - lastChange < thresholdSeconds)
                    continue;

                CrashTelemetryBuffer.ReportRuntimeWatchdogStall(
                    unchecked((uint)laneIndex),
                    unchecked((uint)currentCounter));
                if (IsEmergencyResetLane(laneIndex))
                {
                    if (ServiceEmergencyReset(laneIndex))
                    {
                        _lastObservedCounters[laneIndex] = Volatile.Read(ref _heartbeatCounters[laneIndex]);
                        _lastChangeTimes[laneIndex] = now;
                    }
                    else
                    {
                        _lastChangeTimes[laneIndex] = now - thresholdSeconds + EmergencyResetFailureCooldownSeconds;
                    }

                    continue;
                }

                GlobalTelemetryBus.RequestEmergencyFlushAsync();
                Application.Quit(-1);
                return;
            }
        }

        private static bool IsEmergencyResetLane(int laneIndex)
        {
            return laneIndex == (int)RuntimeWatchdogLane.FaunaDirector ||
                   laneIndex == (int)RuntimeWatchdogLane.WorldStreaming;
        }

        private static bool ServiceEmergencyReset(int laneIndex)
        {
            if ((uint)laneIndex >= LaneCapacity)
                return false;

            IEmergencyResetTarget target = _emergencyResetTargets[laneIndex] as IEmergencyResetTarget;
            if (target == null)
                return false;

            try
            {
                target.ServiceEmergencyReset();
                Interlocked.Increment(ref _heartbeatCounters[laneIndex]);
                return true;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
                return false;
            }
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

        private static void PublishCriticalGcSpikeNoThrow(uint spikeHash, uint contextHash, int gen0CollectionsDelta)
        {
            try
            {
                GlobalTelemetryBus.PublishCriticalGcSpike(spikeHash, contextHash, gen0CollectionsDelta);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogException(exception);
#endif
            }
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryRegisterDispatcherLanes()
        {
            TryRegisterUpdatable();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        private void TryUnregisterDispatcherLanes()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrameTick = false;
            }

            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _registeredUpdatable = false;
            }
        }

        private void RefreshRegistryDependenciesCold()
        {
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            for (int slot = 0; slot < RegistryHeartbeatSlotCount; slot++)
            {
                CacheHeartbeatService(
                    (GlobalRegistryServiceSlot)slot,
                    GlobalRegistry.ResolveRegisteredServiceForHeartbeat((GlobalRegistryServiceSlot)slot));
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private static void CacheHeartbeatService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            int slot = (int)serviceSlot;
            if ((uint)slot >= RegistryHeartbeatSlotCount)
                return;

            _registryHeartbeatServices[slot] = currentService;
        }

        private void RebindRegistryDependency(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            CacheHeartbeatService(serviceSlot, currentService);
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterDispatcherLanes();
                if (currentService != null && isActiveAndEnabled && !_runtimeOwnerRejected)
                    TryRegisterDispatcherLanes();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PersistentWorldRegistry)
                _persistentWorldRegistry = currentService as IRuntimeWatchdogWorldHealthBridge;
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            RebindRegistryDependency(serviceSlot, currentService);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            RebindRegistryDependency(serviceSlot, currentService);
        }
    }
}
