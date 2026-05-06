#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Hecton.Localization;
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
        private static readonly uint _Gen0CollectionWarningHash = unchecked((uint)LocHash.Compute("GCMonitor.Gen0CollectionDetected"));
        private static readonly uint _GcMonitorContextHash = unchecked((uint)LocHash.Compute(nameof(GCMonitor)));

        private bool _registeredPostFixed;
        private int _lastGen0CollectionCount;
        private int _lastReportedFrame = -1;
        private int _nextMemoryPressureSampleFrame;
        private int _lastMemoryPressureDispatchFrame = -MemoryPressureSampleIntervalFrames;
        private int _nextNativeLeakAuditFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GlobalRegistry.ClearGCMonitorRuntime(null);
        }

        public static GCMonitor EnsureRuntimeInstance()
        {
            GCMonitor runtime = GlobalRegistry.GCMonitorRuntime;
            if (runtime != null)
                return runtime;

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
            GCMonitor runtime = GlobalRegistry.GCMonitorRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterGCMonitorRuntime(this);
            _lastGen0CollectionCount = GC.CollectionCount(0);
            PrimeSamplingFrames();
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
            GlobalRegistry.ClearGCMonitorRuntime(this);
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
            GlobalTelemetryBus.PublishPerformanceWarning(_Gen0CollectionWarningHash, _GcMonitorContextHash, delta);
            Debug.LogAssertion("[GCMonitor] Gen0 GC collection detected. Telemetry emitted.");
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
#else
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    public sealed class GCMonitor : MonoBehaviour
    {
        public static GCMonitor EnsureRuntimeInstance()
        {
            return null;
        }

        public void InitializeService()
        {
        }
    }
}
#endif
