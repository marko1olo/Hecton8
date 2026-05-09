using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Narrative
{
    public enum AudioLogEventType : byte
    {
        Discovered = 0,
        PlaybackStarted = 1,
        PlaybackStopped = 2,
        PlaybackCompleted = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioLogEventPayload
    {
        public AudioLogEventType Type;
        public ulong TimestampTicks;
        public uint LogHash;
        public int ReferenceSlot;
        public float DurationSeconds;
    }

    public interface IAudioLogEventListener
    {
        void OnAudioLogEvent(in AudioLogEventPayload payload);
    }

    public static class AudioLogEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;
        private const int ReferenceSlotCapacity = 128;
        private const uint AudioLogQueueOverflowWarningHash = 0x414C514Fu; // ALQO
        private const uint AudioLogReferenceSlotOverflowWarningHash = 0x414C5253u; // ALRS
        private const uint AudioLogListenerRejectedWarningHash = 0x414C524Au; // ALRJ
        private const uint AudioLogListenerExceptionWarningHash = 0x414C4558u; // ALEX
        private const uint AudioLogQueueContextHash = 0x414C5155u; // ALQU
        private const uint AudioLogReferenceSlotContextHash = 0x414C5246u; // ALRF
        private const uint AudioLogListenerContextHash = 0x414C4953u; // ALIS
        // COLD ALLOC: RegistryBucket<IAudioLogEventListener>[16] — audio log event listener registry drained on dispatcher LateUpdate — owner: AudioLogEvents
        private static readonly RegistryBucket<IAudioLogEventListener> _listeners = new RegistryBucket<IAudioLogEventListener>(ListenerCapacity);
        // COLD ALLOC: IAudioLogEventListener[16] — listener additions deferred while dispatching audio-log events — owner: AudioLogEvents
        private static readonly IAudioLogEventListener[] _deferredRegisterListeners = new IAudioLogEventListener[ListenerCapacity];
        // COLD ALLOC: IAudioLogEventListener[16] — listener removals deferred while dispatching audio-log events — owner: AudioLogEvents
        private static readonly IAudioLogEventListener[] _deferredUnregisterListeners = new IAudioLogEventListener[ListenerCapacity];
        private struct AudioLogReferenceSlot
        {
            public AudioLogData LogData;

            public void Clear()
            {
                LogData = null;
            }
        }

        // COLD ALLOC: AudioLogReferenceSlot[128] — managed audio-log data sidecar resolved only during dispatch — owner: AudioLogEvents
        private static readonly AudioLogReferenceSlot[] _referenceSlots = new AudioLogReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[128] — audio-log sidecar occupancy map — owner: AudioLogEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<AudioLogEventPayload> _pendingEvents;
        private static NativeQueue<AudioLogEventPayload> _nextFrameEvents;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedReferenceSlotCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastReferenceSlotOverflowTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioLogEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioLogEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedReferenceSlotCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastReferenceSlotOverflowTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IAudioLogEventListener listener)
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

        public static void Unregister(IAudioLogEventListener listener)
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

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out AudioLogEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IAudioLogEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IAudioLogEventListener listener = rawArray[i];
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        DispatchToListener(listener, in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static bool TryResolveLogData(in AudioLogEventPayload payload, out AudioLogData data)
        {
            data = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            data = _referenceSlots[payload.ReferenceSlot].LogData;
            return data != null;
        }

        public static void RaiseLogDiscovered(uint logHash, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.Discovered, logHash, 0f, data);
        }

        public static void RaisePlaybackStarted(uint logHash, float durationSeconds, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.PlaybackStarted, logHash, durationSeconds, data);
        }

        public static void RaisePlaybackStopped(uint logHash, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.PlaybackStopped, logHash, 0f, data);
        }

        public static void RaisePlaybackCompleted(uint logHash, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.PlaybackCompleted, logHash, 0f, data);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AudioLogEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] — deferred audio-log event lane flushed by SystemDispatcher LateUpdate — owner: AudioLogEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(AudioLogEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<AudioLogEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] — next-frame audio-log event lane prevents same-frame reentrant dispatch — owner: AudioLogEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(AudioLogEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void Enqueue(AudioLogEventType type, uint logHash, float durationSeconds, AudioLogData data)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(type);
                return;
            }

            int referenceSlot = -1;
            if (data != null)
            {
                if (!TryReserveReferenceSlot(out referenceSlot))
                {
                    ReportReferenceSlotOverflow(type);
                    return;
                }

                _referenceSlots[referenceSlot].LogData = data;
            }

            AudioLogEventPayload payload = new AudioLogEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                LogHash = logHash,
                ReferenceSlot = referenceSlot,
                DurationSeconds = durationSeconds
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
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

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<AudioLogEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out AudioLogEventPayload payload))
                    break;

                if (pendingCount > 0)
                    pendingCount--;

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<AudioLogEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IAudioLogEventListener listener, in AudioLogEventPayload payload)
        {
            try
            {
                listener.OnAudioLogEvent(in payload);
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

        private static void QueueDeferredRegister(IAudioLogEventListener listener)
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

        private static void QueueDeferredUnregister(IAudioLogEventListener listener)
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

        private static bool CancelDeferredRegister(IAudioLogEventListener listener)
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

        private static void CancelDeferredUnregister(IAudioLogEventListener listener)
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

        private static bool IsDeferredRegisterPending(IAudioLogEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IAudioLogEventListener listener)
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
                IAudioLogEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IAudioLogEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IAudioLogEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportQueueOverflow(AudioLogEventType type)
        {
            _droppedEventCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            uint contextHash = AudioLogQueueContextHash ^ ((uint)type << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogQueueOverflowWarningHash,
                contextHash,
                PositiveCount(_droppedEventCount));
        }

        private static void ReportReferenceSlotOverflow(AudioLogEventType type)
        {
            _droppedReferenceSlotCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastReferenceSlotOverflowTelemetryFrame == frame)
                return;

            _lastReferenceSlotOverflowTelemetryFrame = frame;
            uint contextHash = AudioLogReferenceSlotContextHash ^ ((uint)type << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogReferenceSlotOverflowWarningHash,
                contextHash,
                PositiveCount(_droppedReferenceSlotCount));
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogListenerRejectedWarningHash,
                AudioLogListenerContextHash,
                PositiveCount(_droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogListenerExceptionWarningHash,
                AudioLogListenerContextHash,
                PositiveCount(_listenerExceptionCount));
        }

        private static int PositiveCount(int count)
        {
            return count > 0 ? count : 1;
        }

        private static void DropQueuedEvents()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out AudioLogEventPayload payload))
                    ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out AudioLogEventPayload payload))
                    ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
        }

        private static bool TryReserveReferenceSlot(out int slot)
        {
            slot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
                return false;

            for (int attempt = 0; attempt < ReferenceSlotCapacity; attempt++)
            {
                int candidate = _referenceWriteIndex;
                _referenceWriteIndex = (_referenceWriteIndex + 1) % ReferenceSlotCapacity;
                if (_referenceSlotOccupied[candidate])
                    continue;

                _referenceSlotOccupied[candidate] = true;
                _referencePendingCount++;
                slot = candidate;
                return true;
            }

            return false;
        }

        private static bool IsValidReferenceSlot(int slot)
        {
            return (uint)slot < ReferenceSlotCapacity && _referenceSlotOccupied[slot];
        }

        private static void ReleaseReferenceSlot(int slot)
        {
            if (!IsValidReferenceSlot(slot))
                return;

            _referenceSlots[slot].Clear();
            _referenceSlotOccupied[slot] = false;
            if (_referencePendingCount > 0)
                _referencePendingCount--;
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _referenceSlots[i].Clear();
                _referenceSlotOccupied[i] = false;
            }
        }
    }
}
