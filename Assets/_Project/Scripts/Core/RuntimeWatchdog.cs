using System.Threading;
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

        // COLD ALLOC: int[32] - cross-thread liveness counters - owner: RuntimeWatchdog
        private static readonly int[] _heartbeatCounters = new int[LaneCapacity];
        // COLD ALLOC: int[32] - sampled liveness counters - owner: RuntimeWatchdog
        private static readonly int[] _lastObservedCounters = new int[LaneCapacity];
        // COLD ALLOC: double[32] - last heartbeat change timestamps - owner: RuntimeWatchdog
        private static readonly double[] _lastChangeTimes = new double[LaneCapacity];
        // COLD ALLOC: bool[32] - active liveness lane mask - owner: RuntimeWatchdog
        private static readonly bool[] _activeLanes = new bool[LaneCapacity];

        private static RuntimeWatchdog _instance;
        private bool _registeredUpdatable;
        private int _nextSampleFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            System.Array.Clear(_heartbeatCounters, 0, _heartbeatCounters.Length);
            System.Array.Clear(_lastObservedCounters, 0, _lastObservedCounters.Length);
            System.Array.Clear(_lastChangeTimes, 0, _lastChangeTimes.Length);
            System.Array.Clear(_activeLanes, 0, _activeLanes.Length);
        }

        public static RuntimeWatchdog EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[RuntimeWatchdog]"); // COLD ALLOC: GameObject[1] - bootstrap-owned liveness watchdog root - owner: RuntimeWatchdog
            return runtimeRoot.AddComponent<RuntimeWatchdog>();
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
            TryRegisterUpdatable();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _nextSampleFrame = Time.frameCount + SampleIntervalFrames;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
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
            if (_instance == this)
                _instance = null;
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
                Application.Quit(-1);
                return;
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
