using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Core
{
    /// <summary>
    /// Post-fixed Gen0 GC detector for runtime allocation enforcement.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9485)]
    public sealed class GCMonitor : MonoBehaviour, IPostFixedTickable
    {
        private const int MemoryPressureSampleIntervalFrames = 60;
        private const int NativeLeakAuditIntervalFrames = 300;
        private const double CriticalMemoryPressureRatio = 0.85d;

        private static GCMonitor _instance;

        private bool _registeredPostFixed;
        private int _lastGen0CollectionCount;
        private int _lastReportedFrame = -1;
        private int _nextMemoryPressureSampleFrame;
        private int _lastMemoryPressureDispatchFrame = -MemoryPressureSampleIntervalFrames;
        private int _nextNativeLeakAuditFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static GCMonitor EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[GCMonitor]"); // COLD ALLOC: GameObject[1] - bootstrap-owned GC sentinel root - owner: GCMonitor
            return runtimeRoot.AddComponent<GCMonitor>();
        }

        public void InitializeService()
        {
            _lastGen0CollectionCount = GC.CollectionCount(0);
            PrimeSamplingFrames();
            TryRegisterPostFixed();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _lastGen0CollectionCount = GC.CollectionCount(0);
            PrimeSamplingFrames();
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            TryRegisterPostFixed();
        }

        private void Start()
        {
            TryRegisterPostFixed();
        }

        private void OnDisable()
        {
            if (!_registeredPostFixed)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
            _registeredPostFixed = false;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_instance == this)
                _instance = null;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            int frame = Time.frameCount;
            TryDispatchCriticalMemoryPressure(frame);
            TryAuditLongLivedNativeAllocations(frame);

            int currentGen0CollectionCount = GC.CollectionCount(0);
            if (currentGen0CollectionCount == _lastGen0CollectionCount)
                return;

            int delta = currentGen0CollectionCount - _lastGen0CollectionCount;
            _lastGen0CollectionCount = currentGen0CollectionCount;
            if (_lastReportedFrame == frame)
                return;

            _lastReportedFrame = frame;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SystemDispatcher.TryGetLastPostFixedGcAttribution(
                out string ownerName,
                out int attributedFrame,
                out int attributedDelta,
                out int laneIndex,
                out int itemIndex);

            Debug.LogAssertion(
                "[GCMonitor] Gen0 GC collection detected at frame " +
                frame +
                " attributedFrame=" +
                attributedFrame +
                " attributedOwner=" +
                ownerName +
                " attributedDelta=" +
                attributedDelta +
                " lane=" +
                laneIndex +
                " item=" +
                itemIndex +
                " delta=" +
                delta +
                " fixedDeltaTime=" +
                fixedDeltaTime.ToString("0.000000"));
#endif
        }

        private void TryDispatchCriticalMemoryPressure(int frame)
        {
            if (frame < _nextMemoryPressureSampleFrame)
                return;

            _nextMemoryPressureSampleFrame = frame + MemoryPressureSampleIntervalFrames;
            int physicalMemoryMb = SystemInfo.systemMemorySize;
            if (physicalMemoryMb <= 0)
                return;

            long physicalMemoryBytes = (long)physicalMemoryMb * 1024L * 1024L;
            long reservedMemoryBytes = Profiler.GetTotalReservedMemoryLong();
            double usageRatio = physicalMemoryBytes > 0L
                ? reservedMemoryBytes / (double)physicalMemoryBytes
                : 0d;
            if (usageRatio < CriticalMemoryPressureRatio)
                return;

            if (frame - _lastMemoryPressureDispatchFrame < MemoryPressureSampleIntervalFrames)
                return;

            _lastMemoryPressureDispatchFrame = frame;
            CriticalMemoryPressureEvent memoryPressureEvent = new CriticalMemoryPressureEvent(
                frame,
                reservedMemoryBytes,
                physicalMemoryBytes,
                usageRatio);
            SystemDispatcher.DispatchCriticalMemoryPressure(in memoryPressureEvent);
        }

        private void TryAuditLongLivedNativeAllocations(int frame)
        {
            if (frame < _nextNativeLeakAuditFrame)
                return;

            _nextNativeLeakAuditFrame = frame + NativeLeakAuditIntervalFrames;
            NativeMemorySentinel.AuditLongLivedTransientAllocations(frame);
        }

        private void PrimeSamplingFrames()
        {
            int frame = Time.frameCount;
            _nextMemoryPressureSampleFrame = frame + MemoryPressureSampleIntervalFrames;
            _nextNativeLeakAuditFrame = frame + NativeLeakAuditIntervalFrames;
            _lastMemoryPressureDispatchFrame = frame - MemoryPressureSampleIntervalFrames;
        }

        private void TryRegisterPostFixed()
        {
            if (_registeredPostFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Core);
            _registeredPostFixed = SystemDispatcher
                .GetPostFixedLane(PriorityLayer.Core)
                .Contains(this);
        }
    }
}
