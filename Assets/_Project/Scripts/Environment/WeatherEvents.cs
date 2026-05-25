using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Environment
{
    public enum WeatherEventType : byte
    {
        SnapshotUpdated = 0,
        Lightning = 1
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct WeatherEventPayload
    {
        [FieldOffset(0)] public float3 GlobalCurrentVector;
        [FieldOffset(12)] public float3 GlobalWindVector;
        [FieldOffset(24)] public uint StateMask;
        [FieldOffset(28)] public float WeatherIntensity;
        [FieldOffset(32)] public CurrentMeta CurrentMeta;
        [FieldOffset(64)] public ushort EventType;
        [FieldOffset(66)] public ushort Reserved;
        [FieldOffset(68)] private uint _pad0;
        [FieldOffset(72)] private ulong _pad1;
        [FieldOffset(80)] private ulong _pad2;
        [FieldOffset(88)] private ulong _pad3;
        [FieldOffset(96)] private ulong _pad4;
        [FieldOffset(104)] private ulong _pad5;
        [FieldOffset(112)] private ulong _pad6;
        [FieldOffset(120)] private ulong _pad7;
    }

    public interface IWeatherEventListener
    {
        void OnWeatherEvent(in WeatherEventPayload payload);
    }

    public static class WeatherEvents
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 32;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint EventOverflowWarningHash = 0x57454F46u;
        private const uint ListenerRejectedWarningHash = 0x5745524Au;
        private const uint ListenerExceptionWarningHash = 0x57454558u;
        private const uint EventContextHash = 0x5745534Eu;
        private const uint ListenerContextHash = 0x57454C53u;

        private struct ListenerSlot
        {
            public IWeatherEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct WeatherListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public WeatherListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[32] - fixed weather listener slots drained on dispatcher LateUpdate - owner: WeatherEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IWeatherEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IWeatherEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IWeatherEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return true;
                }

                return false;
            }

            public IWeatherEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static WeatherListenerRegistry _listeners = new WeatherListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[32] - listener additions deferred while dispatching weather events - owner: WeatherEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[32] - listener removals deferred while dispatching weather events - owner: WeatherEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<WeatherEventPayload> _pendingEvents;
        private static NativeQueue<WeatherEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastEventOverflowTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(WeatherEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(WeatherEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastEventOverflowTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IWeatherEventListener listener)
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

        public static void Unregister(IWeatherEventListener listener)
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
                DropPendingAmbient();
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

                if (!_pendingEvents.TryDequeue(out WeatherEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IWeatherEventListener listener = _listeners.GetAt(i);
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
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        [Obsolete("Use TryRaiseSnapshotUpdated(in WeatherRuntimeSnapshot) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseSnapshotUpdated(in WeatherRuntimeSnapshot snapshot)
        {
            TryRaiseSnapshotUpdated(in snapshot);
        }

        public static bool TryRaiseSnapshotUpdated(in WeatherRuntimeSnapshot snapshot)
        {
            EnsureInitialized();
            WeatherEventPayload payload = new WeatherEventPayload
            {
                GlobalCurrentVector = snapshot.GlobalCurrentVector,
                GlobalWindVector = snapshot.GlobalWindVector,
                CurrentMeta = snapshot.CurrentMeta,
                StateMask = (uint)snapshot.StateMask,
                WeatherIntensity = snapshot.WeatherIntensity,
                EventType = (ushort)WeatherEventType.SnapshotUpdated,
                Reserved = 0
            };

            return EnqueuePayload(in payload);
        }

        [Obsolete("Use TryRaiseLightning(float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseLightning(float flashIntensity01)
        {
            TryRaiseLightning(flashIntensity01);
        }

        public static bool TryRaiseLightning(float flashIntensity01)
        {
            EnsureInitialized();
            WeatherEventPayload payload = new WeatherEventPayload
            {
                GlobalCurrentVector = default,
                GlobalWindVector = default,
                CurrentMeta = default,
                StateMask = (uint)WeatherState.Storm,
                WeatherIntensity = math.saturate(flashIntensity01),
                EventType = (ushort)WeatherEventType.Lightning,
                Reserved = 0
            };

            return EnqueuePayload(in payload);
        }

        private static bool EnqueuePayload(in WeatherEventPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventOverflow();
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        public static void DropPendingAmbient()
        {
            DrainQueueImmediate(ref _pendingEvents);
            DrainQueueImmediate(ref _nextFrameEvents);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<WeatherEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<WeatherEventPayload>[32] — deferred weather event lane — owner: WeatherEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(WeatherEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<WeatherEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<WeatherEventPayload>[32] — next-frame weather event lane prevents same-frame reentrant dispatch — owner: WeatherEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(WeatherEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
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

        private static void DispatchToListener(IWeatherEventListener listener, in WeatherEventPayload payload)
        {
            try
            {
                listener.OnWeatherEvent(in payload);
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
            Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IWeatherEventListener listener)
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IWeatherEventListener listener)
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IWeatherEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IWeatherEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IWeatherEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IWeatherEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IWeatherEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IWeatherEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropPendingAmbient();
        }

        private static void RegisterImmediate(IWeatherEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportEventOverflow()
        {
            _droppedEventCount++;
            int frame = Time.frameCount;
            if (_lastEventOverflowTelemetryFrame == frame)
                return;

            _lastEventOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                EventOverflowWarningHash,
                EventContextHash,
                Mathf.Max(1, _droppedEventCount));
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerRejectedWarningHash,
                ListenerContextHash,
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
                ListenerExceptionWarningHash,
                ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<WeatherEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                {
                    pendingCount = 0;
                    break;
                }

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void DrainQueueImmediate(ref NativeQueue<WeatherEventPayload> queue)
        {
            if (!queue.IsCreated)
                return;

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<WeatherEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
