using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.UI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Runtime liveness monitor and deterministic load-shed enforcer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9490)]
    public sealed class RuntimeWatchdog : MonoBehaviour, IUpdatable
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
        private const int FrameStripConsecutiveFrames = 5;
        private const int FaunaEmergencyCullCooldownFrames = 60;
        private const double StallThresholdSeconds = 5.0;
        private const double FrozenServiceThresholdSeconds = 2.0;
        private const float BaseFrameStripThresholdMs = 15f;
        private const float BaseFrameStripThresholdSeconds = BaseFrameStripThresholdMs / 1000f;
        private const float XrFrameStripThresholdSeconds = BaseFrameStripThresholdSeconds * 0.5f;
        private const float GlobalLodBiasEmergency = 0.5f;
        private const float BaseFaunaArteryBudgetMs = 2.0f;
        private const float BaseHudHeartbeatTimeoutSeconds = 0.2f;
        private const float MmfHealthCheckIntervalSeconds = 60f;
        private const long BaseMmfBloatThresholdBytes = 50L * 1024L * 1024L;
        private const long InvalidMmfSectorHash = long.MinValue;
        private const int FaunaLogicRateColdTick = 1;
        private const int GetFileExInfoStandard = 0;
        private const uint WatchdogStateGlobalLodStripped = 1u << 0;
        private const string DeadlockTraceFilePrefix = "runtime_watchdog_deadlock_";
        private const string DeadlockTraceFileExtension = ".txt";

        private static readonly int _globalLodBiasId = Shader.PropertyToID("_GlobalLodBias");
        private static readonly int _faunaLogicRateId = Shader.PropertyToID("_FaunaLogicRate");
        private static readonly uint _watchdogContextHash = unchecked((uint)LocHash.Compute(nameof(RuntimeWatchdog)));
        private static readonly uint _budgetStripHash = unchecked((uint)LocHash.Compute("WATCHDOG_BUDGET_STRIP"));
        private static readonly uint _faunaEmergencyCullHash = unchecked((uint)LocHash.Compute("FAUNA_EMERGENCY_CULL"));
        private static readonly uint _mmfBloatAlarmHash = unchecked((uint)LocHash.Compute("MMF_BLOAT_ALARM"));
        private static readonly uint _uiDeadlockHash = unchecked((uint)LocHash.Compute("UI_DEADLOCK"));
        private static readonly uint _nativeLeakReapedHash = unchecked((uint)LocHash.Compute("NATIVE_LEAK_REAPED"));
        private static readonly uint _nativeLeakLabelHash = unchecked((uint)LocHash.Compute("NATIVE_LEAK_LABEL"));
        private static readonly uint _nanSentinelRecoveryHash = unchecked((uint)LocHash.Compute("NAN_SENTINEL_RECOVERY"));
        private static readonly long _baseFaunaArteryBudgetTicks = MillisecondsToStopwatchTicks(BaseFaunaArteryBudgetMs);
        private static readonly long _xrFaunaArteryBudgetTicks = Math.Max(1L, _baseFaunaArteryBudgetTicks >> 1);

        // COLD ALLOC: int[32] - cross-thread liveness counters - owner: RuntimeWatchdog
        private static readonly int[] _heartbeatCounters = new int[LaneCapacity];
        // COLD ALLOC: int[32] - sampled liveness counters - owner: RuntimeWatchdog
        private static readonly int[] _lastObservedCounters = new int[LaneCapacity];
        // COLD ALLOC: double[32] - last heartbeat change timestamps - owner: RuntimeWatchdog
        private static readonly double[] _lastChangeTimes = new double[LaneCapacity];
        // COLD ALLOC: bool[32] - active liveness lane mask - owner: RuntimeWatchdog
        private static readonly bool[] _activeLanes = new bool[LaneCapacity];
        // COLD ALLOC: IEmergencyResetTarget[32] - frozen-service recovery callback table - owner: RuntimeWatchdog
        private static readonly IEmergencyResetTarget[] _emergencyResetTargets = new IEmergencyResetTarget[LaneCapacity];

        // COLD ALLOC: object[1] - MMF background size result synchronization - owner: RuntimeWatchdog
        private static readonly object _mmfHealthResultLock = new object();
        // COLD ALLOC: WaitCallback[1] - MMF background size probe entry point - owner: RuntimeWatchdog
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
        private uint _watchdogStateFlags;
        private int _nextSampleFrame;
        private int _consecutiveOverBudgetFrames;
        private int _lastConsumedMmfHealthGeneration;
        private double _nextMmfHealthCheckTime;
        private long _lastMmfBytes = -1L;
        private long _lastMmfSectorHash = InvalidMmfSectorHash;

        public static int ActiveTargetFPS => HectonXRRuntimeState.IsXRActive ? VrTargetFPS : TargetFPS;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Array.Clear(_heartbeatCounters, 0, _heartbeatCounters.Length);
            Array.Clear(_lastObservedCounters, 0, _lastObservedCounters.Length);
            Array.Clear(_lastChangeTimes, 0, _lastChangeTimes.Length);
            Array.Clear(_activeLanes, 0, _activeLanes.Length);
            Array.Clear(_emergencyResetTargets, 0, _emergencyResetTargets.Length);
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

            GlobalTelemetryBus.PublishPerformanceWarning(
                _nativeLeakReapedHash,
                _watchdogContextHash,
                reapedCount);
        }

        internal static void ReportNativeLeakReaped(uint ownerHash, uint labelHash, long bytes)
        {
            float megabytes = bytes <= 0L ? 0f : math.min(float.MaxValue, bytes / (1024f * 1024f));
            GlobalTelemetryBus.PublishPerformanceWarning(_nativeLeakReapedHash, ownerHash, megabytes);
            if (labelHash != 0u)
                GlobalTelemetryBus.PublishPerformanceWarning(_nativeLeakLabelHash, labelHash, megabytes);
        }

        internal static Vector3 ReportRigidbodyNanRecovery(
            uint updatingSystemHash,
            Vector3 invalidRuntimePosition,
            Vector3 lastKnownGoodRuntimePosition)
        {
            Vector3 recoveredPosition = CrashTelemetryBuffer.ReportNanPhysicsRecovery(
                invalidRuntimePosition,
                lastKnownGoodRuntimePosition);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _nanSentinelRecoveryHash,
                updatingSystemHash,
                1f);
            return recoveredPosition;
        }

        public void InitializeService()
        {
            GlobalRegistry.RegisterRuntimeWatchdogRuntime(this);
            TryRegisterUpdatable();
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
        }

        private void OnEnable()
        {
            TryRegisterUpdatable();
        }

        private void Start()
        {
            TryRegisterUpdatable();
        }

        private void OnDisable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (ReferenceEquals(GlobalRegistry.RuntimeWatchdog, this))
                GlobalRegistry.UnregisterRuntimeWatchdogRuntime(this);
        }

        public void Tick(float deltaTime)
        {
            int frame = Time.frameCount;
            ConsumeMmfHealthResult();
            EnforceFrameBudget(deltaTime);

            double now = Time.realtimeSinceStartupAsDouble;
            EnforceHudHeartbeat(now, frame);
            QueueMmfHealthCheckIfDue(now);

            if (frame < _nextSampleFrame)
                return;

            _nextSampleFrame = frame + SampleIntervalFrames;
            NativeMemorySentinel.AuditLongLivedTransientAllocations(frame);
            SampleRuntimeLanes(now);
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
            GlobalTelemetryBus.PublishPerformanceWarning(
                _budgetStripHash,
                _watchdogContextHash,
                deltaTime * 1000f);
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
                return;

            Shader.SetGlobalInt(_faunaLogicRateId, FaunaLogicRateColdTick);
            _nextFaunaEmergencyCullFrame = frame + FaunaEmergencyCullCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _faunaEmergencyCullHash,
                _watchdogContextHash,
                elapsedMilliseconds);
        }

        private static float ResolveScaledThreshold(float baseValue)
        {
            return HectonXRRuntimeState.IsXRActive ? baseValue * 0.5f : baseValue;
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

            Canvas.ForceUpdateCanvases();
            _hudDeadlockRecoveryFrame = frame;
            _lastHudCanvasUpdateTime = now;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _uiDeadlockHash,
                _watchdogContextHash,
                (float)(elapsedSeconds * 1000d));
        }

        private void QueueMmfHealthCheckIfDue(double now)
        {
            if (now < _nextMmfHealthCheckTime)
                return;

            _nextMmfHealthCheckTime = now + MmfHealthCheckIntervalSeconds;
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null ||
                !registry.TryGetIndexedSaveHealth(out string savePath, out long sectorHash) ||
                string.IsNullOrEmpty(savePath))
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _mmfHealthCheckInFlight, 1, 0) != 0)
                return;

            _mmfHealthWorkPath = savePath;
            _mmfHealthWorkSectorHash = sectorHash;
            _mmfHealthWorkGeneration = Interlocked.Increment(ref _mmfHealthGeneration);
            ThreadPool.UnsafeQueueUserWorkItem(_mmfHealthCallback, null);
        }

        private static void ExecuteMmfHealthCheck(object state)
        {
            long bytes = -1L;
            long sectorHash = InvalidMmfSectorHash;
            int generation = 0;
            try
            {
                string path = _mmfHealthWorkPath;
                generation = _mmfHealthWorkGeneration;
                sectorHash = _mmfHealthWorkSectorHash;
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
                GlobalTelemetryBus.PublishPerformanceWarning(
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

                double thresholdSeconds = IsEmergencyResetLane(laneIndex)
                    ? FrozenServiceThresholdSeconds
                    : StallThresholdSeconds;
                if (now - lastChange < thresholdSeconds)
                    continue;

                CrashTelemetryBuffer.ReportRuntimeWatchdogStall(
                    unchecked((uint)laneIndex),
                    unchecked((uint)currentCounter));
                if (IsEmergencyResetLane(laneIndex))
                {
                    ServiceEmergencyReset(laneIndex);
                    _lastObservedCounters[laneIndex] = currentCounter;
                    _lastChangeTimes[laneIndex] = now;
                    continue;
                }

                WriteDeadlockTraceDump(laneIndex, currentCounter, now - lastChange);
                Application.Quit(-1);
                return;
            }
        }

        private static bool IsEmergencyResetLane(int laneIndex)
        {
            return laneIndex == (int)RuntimeWatchdogLane.FaunaDirector ||
                   laneIndex == (int)RuntimeWatchdogLane.WorldStreaming;
        }

        private static void ServiceEmergencyReset(int laneIndex)
        {
            if ((uint)laneIndex >= LaneCapacity)
                return;

            IEmergencyResetTarget target = _emergencyResetTargets[laneIndex];
            target?.ServiceEmergencyReset();
            Interlocked.Increment(ref _heartbeatCounters[laneIndex]);
        }

        private static void WriteDeadlockTraceDump(int laneIndex, int counter, double stalledSeconds)
        {
            try
            {
                string directory = ResolveExecutableAdjacentDirectory();
                Directory.CreateDirectory(directory);
                string fileName = DeadlockTraceFilePrefix +
                                  DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
                                  DeadlockTraceFileExtension;
                string path = Path.Combine(directory, fileName);
                string stackTrace = new StackTrace(0, true).ToString();

                if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                {
                    File.WriteAllText(path, stackTrace);
                    return;
                }

                try
                {
                    int length = 0;
                    char[] buffer = lease.Buffer;
                    AppendLiteral(buffer, ref length, "HECTON8_RUNTIME_WATCHDOG_DEADLOCK");
                    AppendLine(buffer, ref length);
                    AppendLiteral(buffer, ref length, "frame=");
                    AppendInt(buffer, ref length, Time.frameCount);
                    AppendLiteral(buffer, ref length, " lane=");
                    AppendInt(buffer, ref length, laneIndex);
                    AppendLiteral(buffer, ref length, " counter=");
                    AppendInt(buffer, ref length, counter);
                    AppendLiteral(buffer, ref length, " stalledSeconds=");
                    AppendDouble(buffer, ref length, stalledSeconds);
                    AppendLine(buffer, ref length);
                    AppendLiteral(buffer, ref length, "mainThreadStack:");
                    AppendLine(buffer, ref length);

                    File.WriteAllText(path, new string(buffer, 0, length));
                    File.AppendAllText(path, stackTrace);
                }
                finally
                {
                    CharBufferPool.Release(lease);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogError("[RuntimeWatchdog] Failed to write deadlock trace dump: " + exception.Message);
#endif
            }
        }

        private static string ResolveExecutableAdjacentDirectory()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            return dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
        }

        private static void AppendLiteral(char[] buffer, ref int length, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length && length < buffer.Length; i++)
                buffer[length++] = value[i];
        }

        private static void AppendLine(char[] buffer, ref int length)
        {
            if (buffer == null || length >= buffer.Length)
                return;

            buffer[length++] = '\n';
        }

        private static void AppendInt(char[] buffer, ref int length, int value)
        {
            if (buffer == null || length >= buffer.Length)
                return;

            if (value == 0)
            {
                buffer[length++] = '0';
                return;
            }

            if (value < 0)
            {
                buffer[length++] = '-';
                value = -value;
            }

            int start = length;
            while (value > 0 && length < buffer.Length)
            {
                int digit = value % 10;
                buffer[length++] = (char)('0' + digit);
                value /= 10;
            }

            int end = length - 1;
            while (start < end)
            {
                char temp = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = temp;
                start++;
                end--;
            }
        }

        private static void AppendDouble(char[] buffer, ref int length, double value)
        {
            if (buffer == null || length >= buffer.Length)
                return;

            int available = buffer.Length - length;
            if (value.TryFormat(
                    buffer.AsSpan(length, available),
                    out int charsWritten,
                    "0.000",
                    CultureInfo.InvariantCulture))
            {
                length += charsWritten;
            }
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }
    }
}
