using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Persistent liveness monitor for dispatcher-adjacent runtime lanes and explicit worker heartbeats.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9490)]
    public sealed class RuntimeWatchdog : MonoBehaviour, IUpdatable
    {
        public enum RuntimeWatchdogLane : byte
        {
            DispatcherUpdate = 0,
            DispatcherLateFrame = 1,
            CrashTelemetry = 2,
            Worker0 = 16,
            Worker1 = 17,
            Worker2 = 18,
            Worker3 = 19,
            Worker4 = 20,
            Worker5 = 21,
            Worker6 = 22,
            Worker7 = 23,
        }

        private const int LaneCapacity = 32;
        private const int SampleIntervalFrames = 60;
        private const double StallThresholdSeconds = 5.0;
        private const string DeadlockTraceFilePrefix = "runtime_watchdog_deadlock_";
        private const string DeadlockTraceFileExtension = ".txt";

        // COLD ALLOC: int[32] - cross-thread liveness counters - owner: RuntimeWatchdog
        private static readonly int[] _heartbeatCounters = new int[LaneCapacity];
        // COLD ALLOC: int[32] - sampled liveness counters - owner: RuntimeWatchdog
        private static readonly int[] _lastObservedCounters = new int[LaneCapacity];
        // COLD ALLOC: double[32] - last heartbeat change timestamps - owner: RuntimeWatchdog
        private static readonly double[] _lastChangeTimes = new double[LaneCapacity];
        // COLD ALLOC: bool[32] - active liveness lane mask - owner: RuntimeWatchdog
        private static readonly bool[] _activeLanes = new bool[LaneCapacity];

        private bool _registeredUpdatable;
        private int _nextSampleFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            System.Array.Clear(_heartbeatCounters, 0, _heartbeatCounters.Length);
            System.Array.Clear(_lastObservedCounters, 0, _lastObservedCounters.Length);
            System.Array.Clear(_lastChangeTimes, 0, _lastChangeTimes.Length);
            System.Array.Clear(_activeLanes, 0, _activeLanes.Length);
        }

        public static RuntimeWatchdog EnsureRuntimeInstance()
        {
            RuntimeWatchdog watchdog = GlobalRegistry.RuntimeWatchdog;
            if (watchdog != null)
                return watchdog;

            GameObject runtimeRoot = new GameObject("[RuntimeWatchdog]"); // COLD ALLOC: GameObject[1] - bootstrap-owned liveness watchdog root - owner: RuntimeWatchdog
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
            if (frame < _nextSampleFrame)
                return;

            _nextSampleFrame = frame + SampleIntervalFrames;
            double now = Time.realtimeSinceStartupAsDouble;
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

                if (now - lastChange < StallThresholdSeconds)
                    continue;

                CrashTelemetryBuffer.ReportRuntimeWatchdogStall(
                    unchecked((uint)laneIndex),
                    unchecked((uint)currentCounter));
                WriteDeadlockTraceDump(laneIndex, currentCounter, now - lastChange);
                Application.Quit(-1);
                return;
            }
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
