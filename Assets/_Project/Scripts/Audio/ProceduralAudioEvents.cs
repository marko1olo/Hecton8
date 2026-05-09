using System;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Audio
{
    public enum ProceduralAudioPingKind : byte
    {
        Sonar = 0,
        PredatorKill = 1,
        MeteorBoom = 2,
        MechanicalWhirr = 3,
        LeviathanRoar = 4
    }

    /// <summary>
    /// Zero-allocation payload for sample-accurate procedural audio triggers.
    /// </summary>
    public readonly struct AudioPingTriggerInfo
    {
        /// <summary>
        /// Creates a new audio ping trigger payload.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Audible chirp duration in seconds.</param>
        public AudioPingTriggerInfo(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            StartSampleFrame = startSampleFrame;
            SampleRate = sampleRate > 0 ? sampleRate : 1;
            Intensity = intensity;
            ChirpDurationSeconds = chirpDurationSeconds;
            WorldPosition = Vector3.zero;
            AcousticTransmission01 = 1f;
            LowPassCutoffHz = 22000f;
            Kind = ProceduralAudioPingKind.Sonar;
        }

        public AudioPingTriggerInfo(
            Vector3 worldPosition,
            float intensity,
            float chirpDurationSeconds,
            float acousticTransmission01,
            float lowPassCutoffHz,
            ProceduralAudioPingKind kind)
        {
            StartSampleFrame = 0L;
            SampleRate = 1;
            Intensity = Mathf.Clamp01(intensity);
            ChirpDurationSeconds = Mathf.Max(0f, chirpDurationSeconds);
            WorldPosition = worldPosition;
            AcousticTransmission01 = Mathf.Clamp01(acousticTransmission01);
            LowPassCutoffHz = Mathf.Clamp(lowPassCutoffHz, 80f, 22000f);
            Kind = kind;
        }

        /// <summary>Exact output-sample frame where the ping started rendering.</summary>
        public long StartSampleFrame { get; }

        /// <summary>Audio output sample rate used to resolve the frame timestamp.</summary>
        public int SampleRate { get; }

        /// <summary>Exact start time in seconds derived from the sample-frame clock.</summary>
        public double StartTimeSeconds => StartSampleFrame / (double)SampleRate;

        /// <summary>Normalized ping intensity in the 0..1 range.</summary>
        public float Intensity { get; }

        /// <summary>Primary chirp duration in seconds.</summary>
        public float ChirpDurationSeconds { get; }

        /// <summary>World-space source for diegetic procedural pings.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Acoustic occlusion transmission in the 0..1 range.</summary>
        public float AcousticTransmission01 { get; }

        /// <summary>Low-pass cutoff after acoustic occlusion.</summary>
        public float LowPassCutoffHz { get; }

        /// <summary>Semantic route for procedural rendering.</summary>
        public ProceduralAudioPingKind Kind { get; }
    }

    /// <summary>
    /// Zero-allocation payload for habitat structural stress groan synthesis.
    /// </summary>
    public readonly struct StructuralStressAudioInfo
    {
        /// <summary>
        /// Creates a structural stress audio payload.
        /// </summary>
        public StructuralStressAudioInfo(Vector3 worldPosition, float stress01, float pitchScale)
        {
            WorldPosition = worldPosition;
            Stress01 = Mathf.Clamp01(stress01);
            PitchScale = Mathf.Max(0.1f, pitchScale);
        }

        /// <summary>World-space origin for spatial routing.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Normalized edge stress in the [0..1] range.</summary>
        public float Stress01 { get; }

        /// <summary>Pitch multiplier consumed by the renderer.</summary>
        public float PitchScale { get; }
    }

    /// <summary>
    /// Listener contract for deferred procedural audio notifications.
    /// </summary>
    public interface IProceduralAudioEventListener
    {
        /// <summary>Called after the procedural renderer starts a sonar ping.</summary>
        /// <param name="info">Sample-accurate ping payload.</param>
        void OnAudioPingTriggered(in AudioPingTriggerInfo info);

        /// <summary>Called when a structure emits audible stress.</summary>
        /// <param name="info">Structural stress audio payload.</param>
        void OnStructuralStressTriggered(in StructuralStressAudioInfo info);
    }

    /// <summary>
    /// Queue-backed main-thread bridge for sample-accurate procedural audio triggers.
    /// </summary>
    public static class ProceduralAudioEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingAudioPingCapacity = 8;
        private const int PendingStructuralStressCapacity = 8;
        private static readonly uint _overflowWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.Overflow"));
        private static readonly uint _audioPingQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.AudioPing"));
        private static readonly uint _structuralStressQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.StructuralStress"));
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.ListenerException"));
        private static readonly uint _listenerContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.Listeners"));

        // COLD ALLOC: RegistryBucket<IProceduralAudioEventListener>[8] - deferred procedural-audio listeners - owner: ProceduralAudioEvents
        private static readonly RegistryBucket<IProceduralAudioEventListener> _listeners = new RegistryBucket<IProceduralAudioEventListener>(ListenerCapacity);
        // COLD ALLOC: IProceduralAudioEventListener[8] - listener additions deferred while dispatching procedural audio events - owner: ProceduralAudioEvents
        private static readonly IProceduralAudioEventListener[] _deferredRegisterListeners = new IProceduralAudioEventListener[ListenerCapacity];
        // COLD ALLOC: IProceduralAudioEventListener[8] - listener removals deferred while dispatching procedural audio events - owner: ProceduralAudioEvents
        private static readonly IProceduralAudioEventListener[] _deferredUnregisterListeners = new IProceduralAudioEventListener[ListenerCapacity];
        private static NativeQueue<AudioPingTriggerInfo> _pendingAudioPings;
        private static NativeQueue<AudioPingTriggerInfo> _nextFrameAudioPings;
        private static NativeQueue<StructuralStressAudioInfo> _pendingStructuralStress;
        private static NativeQueue<StructuralStressAudioInfo> _nextFrameStructuralStress;
        private static int _pendingAudioPingCount;
        private static int _nextFrameAudioPingCount;
        private static int _pendingStructuralStressCount;
        private static int _nextFrameStructuralStressCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedAudioPingCount;
        private static int _droppedStructuralStressCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Number of procedural audio events waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                return _pendingAudioPingCount
                    + _nextFrameAudioPingCount
                    + _pendingStructuralStressCount
                    + _nextFrameStructuralStressCount;
            }
        }

        internal static int DroppedAudioPingCount => _droppedAudioPingCount;

        internal static int DroppedStructuralStressCount => _droppedStructuralStressCount;
        internal static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        internal static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingAudioPings.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_pendingAudioPings));
                _pendingAudioPings.Dispose();
                _pendingAudioPings = default;
            }

            if (_nextFrameAudioPings.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_nextFrameAudioPings));
                _nextFrameAudioPings.Dispose();
                _nextFrameAudioPings = default;
            }

            if (_pendingStructuralStress.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_pendingStructuralStress));
                _pendingStructuralStress.Dispose();
                _pendingStructuralStress = default;
            }

            if (_nextFrameStructuralStress.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_nextFrameStructuralStress));
                _nextFrameStructuralStress.Dispose();
                _nextFrameStructuralStress = default;
            }

            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingAudioPingCount = 0;
            _nextFrameAudioPingCount = 0;
            _pendingStructuralStressCount = 0;
            _nextFrameStructuralStressCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedAudioPingCount = 0;
            _droppedStructuralStressCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
            _listeners.Clear();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static void ResetForSmokeTest()
        {
            ResetStaticState();
        }
#endif

        /// <summary>
        /// Registers one procedural audio listener.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void Register(IProceduralAudioEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters one procedural audio listener.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void Unregister(IProceduralAudioEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!_listeners.TryUnregister(listener))
                return;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        /// <summary>
        /// Flushes queued procedural audio notifications on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listeners.Count <= 0)
                {
                    DropQueuedEvents();
                    completed = true;
                }
                else
                {
                    completed = FlushAudioPings();
                    if (completed)
                        completed = FlushStructuralStress();
                }
            }
            finally
            {
                _isDispatching = false;
                ApplyDeferredListenerMutations();
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        /// <summary>
        /// Queues the sample-accurate sonar-ping notification on the main thread.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Primary chirp duration in seconds.</param>
        public static void RaiseAudioPingTriggered(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
            {
                ReportAudioPingOverflow();
                return;
            }

            AudioPingTriggerInfo info = new AudioPingTriggerInfo(startSampleFrame, sampleRate, intensity, chirpDurationSeconds);
            EnqueueAudioPing(in info);
        }

        public static void RaiseAudioPingTriggered(
            Vector3 worldPosition,
            float intensity,
            float chirpDurationSeconds,
            float acousticTransmission01,
            float lowPassCutoffHz,
            ProceduralAudioPingKind kind)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
            {
                ReportAudioPingOverflow();
                return;
            }

            AudioPingTriggerInfo info = new AudioPingTriggerInfo(
                worldPosition,
                intensity,
                chirpDurationSeconds,
                acousticTransmission01,
                lowPassCutoffHz,
                kind);
            EnqueueAudioPing(in info);
        }

        private static void EnqueueAudioPing(in AudioPingTriggerInfo info)
        {
            if (_isDispatching)
            {
                _nextFrameAudioPings.Enqueue(info);
                _nextFrameAudioPingCount++;
            }
            else
            {
                _pendingAudioPings.Enqueue(info);
                _pendingAudioPingCount++;
            }
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        public static void RaiseStructuralStressTriggered(Vector3 worldPosition, float stress01, float pitchScale)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingStructuralStressCount + _nextFrameStructuralStressCount >= PendingStructuralStressCapacity)
            {
                ReportStructuralStressOverflow();
                return;
            }

            StructuralStressAudioInfo info = new StructuralStressAudioInfo(worldPosition, stress01, pitchScale);
            if (_isDispatching)
            {
                _nextFrameStructuralStress.Enqueue(info);
                _nextFrameStructuralStressCount++;
            }
            else
            {
                _pendingStructuralStress.Enqueue(info);
                _pendingStructuralStressCount++;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingAudioPings.IsCreated)
            {
                _pendingAudioPings = new NativeQueue<AudioPingTriggerInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioPingTriggerInfo>[8] - deferred procedural ping lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingAudioPings,
                    PendingAudioPingCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_pendingAudioPings),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingAudioPings, PendingAudioPingCapacity);
            }

            if (!_nextFrameAudioPings.IsCreated)
            {
                _nextFrameAudioPings = new NativeQueue<AudioPingTriggerInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioPingTriggerInfo>[8] - next-frame procedural ping lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameAudioPings,
                    PendingAudioPingCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_nextFrameAudioPings),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameAudioPings, PendingAudioPingCapacity);
            }

            if (!_pendingStructuralStress.IsCreated)
            {
                _pendingStructuralStress = new NativeQueue<StructuralStressAudioInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StructuralStressAudioInfo>[8] - deferred structural stress audio lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingStructuralStress,
                    PendingStructuralStressCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_pendingStructuralStress),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingStructuralStress, PendingStructuralStressCapacity);
            }

            if (!_nextFrameStructuralStress.IsCreated)
            {
                _nextFrameStructuralStress = new NativeQueue<StructuralStressAudioInfo>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StructuralStressAudioInfo>[8] - next-frame structural stress audio lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameStructuralStress,
                    PendingStructuralStressCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_nextFrameStructuralStress),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameStructuralStress, PendingStructuralStressCapacity);
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

        private static bool FlushAudioPings()
        {
            if (!_pendingAudioPings.IsCreated)
                return true;

            int scanBudget = _pendingAudioPingCount > 0 ? _pendingAudioPingCount : PendingAudioPingCapacity;
            while (scanBudget > 0 && !_pendingAudioPings.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingAudioPings.TryDequeue(out AudioPingTriggerInfo info))
                    return true;

                _pendingAudioPingCount--;
                scanBudget--;
                IProceduralAudioEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IProceduralAudioEventListener listener = rawArray[i];
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchAudioPingToListener(listener, in info);
                }
            }

            if (_pendingAudioPings.IsEmpty())
                _pendingAudioPingCount = 0;

            return true;
        }

        private static bool FlushStructuralStress()
        {
            if (!_pendingStructuralStress.IsCreated)
                return true;

            int scanBudget = _pendingStructuralStressCount > 0 ? _pendingStructuralStressCount : PendingStructuralStressCapacity;
            while (scanBudget > 0 && !_pendingStructuralStress.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingStructuralStress.TryDequeue(out StructuralStressAudioInfo info))
                    return true;

                _pendingStructuralStressCount--;
                scanBudget--;
                IProceduralAudioEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IProceduralAudioEventListener listener = rawArray[i];
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchStructuralStressToListener(listener, in info);
                }
            }

            if (_pendingStructuralStress.IsEmpty())
                _pendingStructuralStressCount = 0;

            return true;
        }

        private static void DropQueuedEvents()
        {
            if (_pendingAudioPings.IsCreated)
            {
                while (_pendingAudioPings.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameAudioPings.IsCreated)
            {
                while (_nextFrameAudioPings.TryDequeue(out _))
                {
                }
            }

            if (_pendingStructuralStress.IsCreated)
            {
                while (_pendingStructuralStress.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameStructuralStress.IsCreated)
            {
                while (_nextFrameStructuralStress.TryDequeue(out _))
                {
                }
            }

            _pendingAudioPingCount = 0;
            _nextFrameAudioPingCount = 0;
            _pendingStructuralStressCount = 0;
            _nextFrameStructuralStressCount = 0;
        }

        private static void DispatchAudioPingToListener(IProceduralAudioEventListener listener, in AudioPingTriggerInfo info)
        {
            try
            {
                listener.OnAudioPingTriggered(in info);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        private static void DispatchStructuralStressToListener(IProceduralAudioEventListener listener, in StructuralStressAudioInfo info)
        {
            try
            {
                listener.OnStructuralStressTriggered(in info);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IProceduralAudioEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IProceduralAudioEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i], listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount] = null;
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount] = null;
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IProceduralAudioEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IProceduralAudioEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IProceduralAudioEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _listenerRejectedWarningHash,
                _listenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _listenerExceptionWarningHash,
                _listenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingAudioPings.IsCreated && !_pendingAudioPings.IsEmpty())
                || (_pendingStructuralStress.IsCreated && !_pendingStructuralStress.IsEmpty());
        }

        private static void ReportAudioPingOverflow()
        {
            _droppedAudioPingCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _audioPingQueueHash, PendingAudioPingCapacity);
        }

        private static void ReportStructuralStressOverflow()
        {
            _droppedStructuralStressCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _structuralStressQueueHash, PendingStructuralStressCapacity);
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameAudioPings.IsCreated)
            {
                while (_nextFrameAudioPingCount > 0 && _nextFrameAudioPings.TryDequeue(out AudioPingTriggerInfo info))
                {
                    _nextFrameAudioPingCount--;
                    _pendingAudioPings.Enqueue(info);
                    _pendingAudioPingCount++;
                }
            }

            if (_nextFrameStructuralStress.IsCreated)
            {
                while (_nextFrameStructuralStressCount > 0 && _nextFrameStructuralStress.TryDequeue(out StructuralStressAudioInfo info))
                {
                    _nextFrameStructuralStressCount--;
                    _pendingStructuralStress.Enqueue(info);
                    _pendingStructuralStressCount++;
                }
            }
        }
    }
}
