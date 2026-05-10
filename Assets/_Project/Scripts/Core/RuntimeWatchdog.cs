using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.UI;
using Hecton8.World;
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
    public sealed class RuntimeWatchdog : MonoBehaviour, IUpdatable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        public interface IEmergencyResetTarget
        {
            void ServiceEmergencyReset();
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

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32FileAttributeData
        {
            public uint FileAttributes;
            public uint CreationTimeLow;
            public uint CreationTimeHigh;
            public uint LastAccessTimeLow;
            public uint LastAccessTimeHigh;
            public uint LastWriteTimeLow;
            public uint LastWriteTimeHigh;
            public uint FileSizeHigh;
            public uint FileSizeLow;
        }

        public const int TargetFPS = 60;
        public const int VrTargetFPS = 72;

        private const int LaneCapacity = 32;
        private const int SampleIntervalFrames = 60;
        private const int FrameStripConsecutiveFrames = 3;
        private const int FaunaEmergencyCullCooldownFrames = 60;
        private const int FaunaEmergencyCullEmptyCooldownFrames = 15;
        private const double EmergencyResetFailureCooldownSeconds = 1.0d;
        private const double StallThresholdSeconds = 5.0;
        private const double FrozenServiceThresholdSeconds = 2.0;
        private const double RegistryHeartbeatGuardIntervalSeconds = 60.0d;
        private const float BaseFrameStripThresholdSeconds = 0.01667f;
        private const float XrFrameStripThresholdSeconds = 0.0075f;
        private const float GlobalLodBiasEmergency = 0.5f;
        private const float BaseFaunaArteryBudgetMs = 2.0f;
        private const float BaseHudHeartbeatTimeoutSeconds = 0.2f;
        private const float GcSteadyStateWarmupSeconds = 5f;
        private const float MmfHealthCheckIntervalSeconds = 60f;
        private const float MmfHealthRetryDelaySeconds = 5f;
        private const long BaseMmfBloatThresholdBytes = 50L * 1024L * 1024L;
        private const long RuntimeMemorySpikeThresholdBytes = 50L * 1024L * 1024L;
        private const int MemorySpikeSampleIntervalFrames = 10;
        private const long InvalidMmfSectorHash = long.MinValue;
        private const int RegistryHeartbeatSlotCount = (int)GlobalRegistryServiceSlot.Unknown;
        private const int FaunaLogicRateColdTick = 1;
        private const int GetFileExInfoStandard = 0;
        private const uint WatchdogStateGlobalLodStripped = 1u << 0;

        private static readonly int _globalLodBiasId = Shader.PropertyToID("_GlobalLodBias");
        private static readonly int _faunaLogicRateId = Shader.PropertyToID("_FaunaLogicRate");
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
        // COLD ALLOC: IEmergencyResetTarget[32] — frozen-service recovery callback table — owner: RuntimeWatchdog
        private static readonly IEmergencyResetTarget[] _emergencyResetTargets = new IEmergencyResetTarget[LaneCapacity];
        // COLD ALLOC: int[255] - registry service TickCount samples - owner: RuntimeWatchdog
        private static readonly int[] _registryHeartbeatTicks = new int[RegistryHeartbeatSlotCount];
        // COLD ALLOC: byte[255] - active registry heartbeat sample mask - owner: RuntimeWatchdog
        private static readonly byte[] _registryHeartbeatActive = new byte[RegistryHeartbeatSlotCount];

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

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetFileAttributesEx(
            string lpFileName,
            int fInfoLevelId,
            out Win32FileAttributeData fileData);
#endif

        private bool _registeredUpdatable;
        private bool _registeredLateFrameTick;
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
        private int _steadyStateGcGen0CollectionsDelta;
        private float _gcSteadyStateWarmupRemaining = GcSteadyStateWarmupSeconds;
        private bool _gcSteadyStateActive;
        private double _nextMmfHealthCheckTime;
        private double _nextRegistryHeartbeatGuardTime;
        private long _lastMmfBytes = -1L;
        private long _lastMmfSectorHash = InvalidMmfSectorHash;
        private long _lastTotalAllocatedMemoryBytes;

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
        }

        public static RuntimeWatchdog EnsureRuntimeInstance()
        {
            RuntimeWatchdog watchdog = GlobalRegistry.RuntimeWatchdog;
            if (watchdog != null)
                return watchdog;

            GameObject runtimeRoot = new GameObject("[RuntimeWatchdog]"); // COLD ALLOC: GameObject[1] — bootstrap-owned watchdog root — owner: RuntimeWatchdog
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

            float elapsedMilliseconds = (float)(elapsedTicks * 1000.0d / Stopwatch.Frequency);
            ForceFaunaEmergencyColdTick(elapsedMilliseconds);
        }

        internal static void MarkHudCanvasUpdated(Canvas canvas)
        {
            if (!Application.isPlaying)
                return;

            if (canvas != null)
                _hudCanvas = canvas;
            _lastHudCanvasUpdateTime = Time.realtimeSinceStartupAsDouble;
        }

        internal static void RegisterEmergencyResetTarget(RuntimeWatchdogLane lane, IEmergencyResetTarget target)
        {
            int laneIndex = (int)lane;
            if ((uint)laneIndex >= LaneCapacity || target == null)
                return;

            _emergencyResetTargets[laneIndex] = target;
            _lastObservedCounters[laneIndex] = Volatile.Read(ref _heartbeatCounters[laneIndex]);
            _lastChangeTimes[laneIndex] = Time.realtimeSinceStartupAsDouble;
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
            float megabytes = bytes <= 0L ? 0f : math.min(float.MaxValue, bytes / (1024f * 1024f));
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
            GlobalTelemetryBus.Initialize();
            MathGuard.Initialize();
            BlackBoxHeartbeatThread.Start();
            GlobalRegistry.RegisterRuntimeWatchdogRuntime(this);
            ResetGcCollectionSentinel();
            ResetMemorySpikeTracker();
            ResetRegistryHeartbeatGuard(Time.realtimeSinceStartupAsDouble);
            TryRegisterUpdatable();
            TryRegisterLateFrameTickable();
        }

        private void Awake()
        {
            RuntimeWatchdog registeredWatchdog = GlobalRegistry.RuntimeWatchdog;
            if (registeredWatchdog != null && registeredWatchdog != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }

            _nextSampleFrame = Time.frameCount + SampleIntervalFrames;
            _nextMmfHealthCheckTime = Time.realtimeSinceStartupAsDouble + MmfHealthCheckIntervalSeconds;
            ResetRegistryHeartbeatGuard(Time.realtimeSinceStartupAsDouble);
            GlobalTelemetryBus.Initialize();
            MathGuard.Initialize();
            ResetGcCollectionSentinel();
            ResetMemorySpikeTracker();
        }

        private void OnEnable()
        {
            BlackBoxHeartbeatThread.Start();
            TryRegisterUpdatable();
            TryRegisterLateFrameTickable();
        }

        private void Start()
        {
            TryRegisterUpdatable();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
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

            BlackBoxHeartbeatThread.Stop();
        }

        private void OnDestroy()
        {
            OnDisable();
            if (ReferenceEquals(GlobalRegistry.RuntimeWatchdog, this))
                GlobalRegistry.UnregisterRuntimeWatchdogRuntime(this);
        }

        public void OnServiceShutdown()
        {
            OnDisable();
            if (ReferenceEquals(GlobalRegistry.RuntimeWatchdog, this))
                GlobalRegistry.UnregisterRuntimeWatchdogRuntime(this);
            _watchdogStateFlags = 0u;
            _consecutiveOverBudgetFrames = 0;
            _lastInputLatencySequence = 0u;
            _lastConsumedMmfHealthGeneration = 0;
            _lastMmfBytes = -1L;
            _lastMmfSectorHash = InvalidMmfSectorHash;
            _nextRegistryHeartbeatGuardTime = 0d;
            _lastMemoryBreachFrame = -1;
            ResetGcCollectionSentinel();
            ResetMemorySpikeTracker();
        }

        public void Tick(float deltaTime)
        {
            BlackBoxHeartbeatThread.Ping();
            int frame = Time.frameCount;
            ConsumeMmfHealthResult();
            FrameTimeWatchdog.Tick();
            EnforceFrameBudget(deltaTime);
            TickGcCollectionSentinel(deltaTime);
            TickMemorySpikeTracker(frame);

            double now = Time.realtimeSinceStartupAsDouble;
            EnforceHudHeartbeat(now, frame);
            QueueMmfHealthCheckIfDue(now);
            SampleRegistryHeartbeatsIfDue(now);

            if (frame < _nextSampleFrame)
                return;

            _nextSampleFrame = frame + SampleIntervalFrames;
            NativeMemorySentinel.AuditLongLivedTransientAllocations(frame);
            SampleRuntimeLanes(now);
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
            int frame = Time.frameCount;
            if (_lastGcSpikeFrame == frame)
                return;

            _lastGcSpikeFrame = frame;
            PublishCriticalGcSpikeNoThrow(_criticalGcSpikeHash, _fastTickSteadyStateHash, delta);
        }

        private void ResetMemorySpikeTracker()
        {
            _lastTotalAllocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            _lastMemorySpikeFrame = -1;
            _lastMemoryBreachFrame = -1;
            _nextMemorySpikeSampleFrame = Time.frameCount + MemorySpikeSampleIntervalFrames;
        }

        private void TickMemorySpikeTracker(int frame)
        {
            if (frame < _nextMemorySpikeSampleFrame)
                return;

            _nextMemorySpikeSampleFrame = frame + MemorySpikeSampleIntervalFrames;
            long currentBytes = Profiler.GetTotalAllocatedMemoryLong();
            long previousBytes = _lastTotalAllocatedMemoryBytes;
            long earlyDeltaBytes = currentBytes > previousBytes && previousBytes > 0L ? currentBytes - previousBytes : 0L;
            uint memoryContextHash = ResolveMemorySpikeFingerprint(previousBytes, currentBytes, earlyDeltaBytes, frame);
            TriggerMemorySubsystemBreachIfUnsafe(currentBytes, ResolveRuntimeMemorySafeBoundBytes(), frame, memoryContextHash);
            if (previousBytes <= 0L)
            {
                _lastTotalAllocatedMemoryBytes = currentBytes;
                return;
            }

            long deltaBytes = currentBytes - previousBytes;
            _lastTotalAllocatedMemoryBytes = currentBytes;
            if (deltaBytes <= ResolveScaledByteThreshold(RuntimeMemorySpikeThresholdBytes))
                return;

            if (_lastMemorySpikeFrame == frame)
                return;

            _lastMemorySpikeFrame = frame;
            uint spikeHash = ResolveMemorySpikeFingerprint(previousBytes, currentBytes, deltaBytes, frame);
            GlobalTelemetryBus.RequestEmergencyFlushAsync();
            CrashTelemetryBuffer.ReportRuntimeMemorySpike(previousBytes, currentBytes, deltaBytes, spikeHash);
            float deltaMegabytes = deltaBytes * (1f / (1024f * 1024f));
            PublishPerformanceWarningNoThrow(_runtimeMemorySpikeHash, spikeHash, deltaMegabytes);
        }

        private void TriggerMemorySubsystemBreachIfUnsafe(long currentBytes, long safeBoundBytes, int frame, uint contextHash)
        {
            if (currentBytes <= safeBoundBytes || _lastMemoryBreachFrame == frame)
                return;

            _lastMemoryBreachFrame = frame;
            SuitHUDV4CanvasOverlay.TriggerMemorySubsystemBreach(contextHash);
            PublishPerformanceWarningNoThrow(
                _memorySubsystemBreachHash,
                contextHash,
                currentBytes * (1f / (1024f * 1024f)));
        }

        private static long ResolveRuntimeMemorySafeBoundBytes()
        {
            long systemMemoryMegabytes = SystemInfo.systemMemorySize;
            if (systemMemoryMegabytes <= 0L)
                return 3L * 1024L * 1024L * 1024L;

            long systemMemoryBytes = systemMemoryMegabytes * 1024L * 1024L;
            long safeBoundBytes = (long)(systemMemoryBytes * 0.75d);
            return Math.Max(ResolveScaledByteThreshold(RuntimeMemorySpikeThresholdBytes), safeBoundBytes);
        }

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

            if ((_watchdogStateFlags & WatchdogStateGlobalLodStripped) != 0u ||
                _consecutiveOverBudgetFrames < FrameStripConsecutiveFrames)
            {
                return;
            }

            Shader.SetGlobalFloat(_globalLodBiasId, GlobalLodBiasEmergency);
            _watchdogStateFlags |= WatchdogStateGlobalLodStripped;
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

        private static void ForceFaunaEmergencyColdTick(float elapsedMilliseconds)
        {
            int frame = Time.frameCount;
            if (frame < _nextFaunaEmergencyCullFrame)
                return;

            FaunaDirector director = FaunaDirector.ActiveRuntimeInstance;
            if (director == null)
                return;

            int culledCount = director.ApplyEmergencyColdTickCull();
            if (culledCount <= 0)
            {
                _nextFaunaEmergencyCullFrame = frame + FaunaEmergencyCullEmptyCooldownFrames;
                return;
            }

            Shader.SetGlobalInt(_faunaLogicRateId, FaunaLogicRateColdTick);
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

        private static long ResolveScaledByteThreshold(long baseValue)
        {
            return HectonXRRuntimeState.IsXRActive ? Math.Max(1L, baseValue >> 1) : baseValue;
        }

        private static long MillisecondsToStopwatchTicks(float milliseconds)
        {
            return Math.Max(1L, (long)(milliseconds * Stopwatch.Frequency / 1000f));
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

            Canvas.ForceUpdateCanvases();
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
                object service = GlobalRegistry.ResolveRegisteredServiceForHeartbeat((GlobalRegistryServiceSlot)slot);
                if (!(service is IServiceHeartbeat heartbeat) ||
                    !heartbeat.IsServiceReady ||
                    heartbeat.HeartbeatState == ServiceHeartbeatState.Failed ||
                    heartbeat.HeartbeatState == ServiceHeartbeatState.Shutdown)
                {
                    _registryHeartbeatActive[slot] = 0;
                    _registryHeartbeatTicks[slot] = 0;
                    continue;
                }

                int tickCount = heartbeat.TickCount;
                if (_registryHeartbeatActive[slot] != 0 && _registryHeartbeatTicks[slot] == tickCount)
                {
                    GlobalTelemetryBus.PublishRegistryHeartbeatStale((uint)slot, unchecked((uint)tickCount));
                    PublishPerformanceWarningNoThrow(_registryHeartbeatStaleHash, (uint)slot, 1f);
                }

                _registryHeartbeatTicks[slot] = tickCount;
                _registryHeartbeatActive[slot] = 1;
            }
        }

        private void QueueMmfHealthCheckIfDue(double now)
        {
            if (now < _nextMmfHealthCheckTime)
                return;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
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
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (GetFileAttributesEx(path, GetFileExInfoStandard, out Win32FileAttributeData data))
            {
                bytes = ((long)data.FileSizeHigh << 32) | data.FileSizeLow;
                return true;
            }

            bytes = -1L;
            return false;
#else
            FileStream stream = null;
            try
            {
                stream = File.OpenRead(path);
                bytes = stream.Length;
            }
            finally
            {
                stream?.Dispose();
            }

            return true;
#endif
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
                    math.min(float.MaxValue, deltaBytes / (1024f * 1024f)));
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

            IEmergencyResetTarget target = _emergencyResetTargets[laneIndex];
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

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }
    }
}
