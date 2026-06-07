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
    public sealed class GCMonitor : MonoBehaviour, IPostFixedTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int MemoryPressureSampleIntervalFrames = 60;
        private const int NativeLeakAuditIntervalFrames = 300;
        private const double CriticalMemoryPressureRatio = 0.85d;

        private bool _registeredPostFixed;
        private bool _hotSwapRegistered;
        private int _nextMemoryPressureSampleFrame;
        private int _lastMemoryPressureDispatchFrame = -MemoryPressureSampleIntervalFrames;
        private int _nextNativeLeakAuditFrame;
        private long _physicalMemoryBytesCold;
        private bool _runtimeOwnerRejected;

        public ServiceHeartbeatState HeartbeatState => _registeredPostFixed ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Booting;
        public bool IsServiceReady => _registeredPostFixed;

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
            if (_runtimeOwnerRejected)
                return;

            RefreshPhysicalMemorySnapshotCold();
            PrimeSamplingFrames();
            TryRegisterPostFixed();
        }

        private void Awake()
        {
            GCMonitor runtime = GlobalRegistry.GCMonitorRuntime;
            if (runtime != null && runtime != this)
            {
                _runtimeOwnerRejected = true;
                Destroy(gameObject);
                return;
            }

            _runtimeOwnerRejected = false;
            GlobalRegistry.RegisterGCMonitorRuntime(this);
            RefreshPhysicalMemorySnapshotCold();
            PrimeSamplingFrames();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerRejected)
                return;

            if (_physicalMemoryBytesCold <= 0L)
                RefreshPhysicalMemorySnapshotCold();

            TryRegisterHotSwapListener();
            TryRegisterPostFixed();
        }

        private void Start()
        {
            if (_runtimeOwnerRejected)
                return;

            TryRegisterPostFixed();
        }

        private void OnDisable()
        {
            TryUnregisterPostFixed();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            OnDisable();
            GlobalRegistry.ClearGCMonitorRuntime(this);
        }

        public void OnServiceShutdown()
        {
            OnDisable();
            GlobalRegistry.ClearGCMonitorRuntime(this);
            _physicalMemoryBytesCold = 0L;
            PrimeSamplingFrames();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterPostFixed();
            if (currentService == null || !isActiveAndEnabled)
                return;

            TryRegisterPostFixed();
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            TryDispatchCriticalMemoryPressure(frame);
            TryAuditLongLivedNativeAllocations(frame);
        }

        private void TryDispatchCriticalMemoryPressure(int frame)
        {
            if (frame < _nextMemoryPressureSampleFrame)
                return;

            _nextMemoryPressureSampleFrame = frame + MemoryPressureSampleIntervalFrames;
            long physicalMemoryBytes = _physicalMemoryBytesCold;
            if (physicalMemoryBytes <= 0L)
                return;

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

        private void RefreshPhysicalMemorySnapshotCold()
        {
            int physicalMemoryMb = SystemInfo.systemMemorySize;
            _physicalMemoryBytesCold = physicalMemoryMb > 0
                ? (long)physicalMemoryMb * 1024L * 1024L
                : 0L;
        }

        private void PrimeSamplingFrames()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            _nextMemoryPressureSampleFrame = frame + MemoryPressureSampleIntervalFrames;
            _nextNativeLeakAuditFrame = frame + NativeLeakAuditIntervalFrames;
            _lastMemoryPressureDispatchFrame = frame - MemoryPressureSampleIntervalFrames;
        }

        private void TryRegisterPostFixed()
        {
            if (_registeredPostFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterPostFixed()
        {
            if (!_registeredPostFixed)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
            _registeredPostFixed = false;
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
