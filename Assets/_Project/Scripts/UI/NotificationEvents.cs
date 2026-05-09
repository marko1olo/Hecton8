using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.UI
{
    public enum NotificationEventSeverity : byte
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NotificationEventPayload
    {
        public uint MessageHash;
        public ushort Severity;
        public ushort Reserved;
    }

    public interface INotificationEventListener
    {
        void OnNotificationEvent(in NotificationEventPayload payload);
    }

    public static class NotificationEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;
        private const uint NotificationListenerOverflowWarningHash = 0x4E45564Cu; // NEVL
        private const uint NotificationListenerContextHash = 0x4E455652u; // NEVR
        private const uint NotificationListenerExceptionWarningHash = 0x4E455645u; // NEVE
        private const uint NotificationListenerExceptionContextHash = 0x4E455658u; // NEVX
        private const uint NotificationQueueOverflowWarningHash = 0x4E455651u; // NEVQ
        private const uint NotificationQueueContextHash = 0x4E455650u; // NEVP
        private const uint NotificationRegisteredMessageMissWarningHash = 0x4E45564Du; // NEVM
        private const uint NotificationRegisteredMessageContextHash = 0x4E455643u; // NEVC

        // COLD ALLOC: RegistryBucket<INotificationEventListener>[8] - HUD notification listeners drained on dispatcher LateUpdate - owner: NotificationEvents
        private static readonly RegistryBucket<INotificationEventListener> _listeners = new RegistryBucket<INotificationEventListener>(ListenerCapacity);
        // COLD ALLOC: INotificationEventListener[8] - listener additions deferred while dispatching notification events - owner: NotificationEvents
        private static readonly INotificationEventListener[] _deferredRegisterListeners = new INotificationEventListener[ListenerCapacity];
        // COLD ALLOC: INotificationEventListener[8] - listener removals deferred while dispatching notification events - owner: NotificationEvents
        private static readonly INotificationEventListener[] _deferredUnregisterListeners = new INotificationEventListener[ListenerCapacity];
        // COLD ALLOC: Dictionary<uint,string>[64] - notification message registry keyed by stable FNV-1a hash for cold-path UI resolution - owner: NotificationEvents
        private static readonly Dictionary<uint, string> _messagesByHash = new Dictionary<uint, string>(64);
        private static NativeQueue<NotificationEventPayload> _pendingEvents;
        private static NativeQueue<NotificationEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _registeredMessageMissCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastRegisteredMessageMissTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Number of notification payloads rejected because both native event lanes were full.
        /// </summary>
        public static int DroppedEventCount => _droppedEventCount;

        /// <summary>
        /// Number of registered notification pushes rejected because the message hash was not registered.
        /// </summary>
        public static int RegisteredMessageMissCount => _registeredMessageMissCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(NotificationEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(NotificationEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _messagesByHash.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _registeredMessageMissCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastRegisteredMessageMissTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(INotificationEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(INotificationEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            _listeners.TryUnregister(listener);
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

                if (!_pendingEvents.TryDequeue(out NotificationEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                INotificationEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        INotificationEventListener listener = rawArray[i];
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

        public static uint ComputeMessageHash(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? 0u
                : unchecked((uint)LocHash.Compute(message));
        }

        public static uint RegisterMessage(string message)
        {
            uint messageHash = ComputeMessageHash(message);
            if (messageHash == 0u)
                return 0u;

            if (!_messagesByHash.ContainsKey(messageHash))
                _messagesByHash.Add(messageHash, message);

            return messageHash;
        }

        public static bool TryResolveMessage(uint messageHash, out string message)
        {
            return _messagesByHash.TryGetValue(messageHash, out message);
        }

        public static void PushInfo(string message)
        {
            Publish(message, NotificationEventSeverity.Info);
        }

        public static void PushWarning(string message)
        {
            Publish(message, NotificationEventSeverity.Warning);
        }

        public static void PushCritical(string message)
        {
            Publish(message, NotificationEventSeverity.Critical);
        }

        internal static void PushRegisteredInfo(uint messageHash)
        {
            PublishRegistered(messageHash, NotificationEventSeverity.Info);
        }

        internal static void PushRegisteredWarning(uint messageHash)
        {
            PublishRegistered(messageHash, NotificationEventSeverity.Warning);
        }

        internal static void PushRegisteredCritical(uint messageHash)
        {
            PublishRegistered(messageHash, NotificationEventSeverity.Critical);
        }

        private static void Publish(string message, NotificationEventSeverity severity)
        {
            uint messageHash = ComputeMessageHash(message);
            if (messageHash == 0u)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(severity);
                return;
            }

            if (!_messagesByHash.ContainsKey(messageHash))
                _messagesByHash.Add(messageHash, message);

            NotificationEventPayload payload = new NotificationEventPayload
            {
                MessageHash = messageHash,
                Severity = (ushort)severity,
                Reserved = 0
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

        private static void PublishRegistered(uint messageHash, NotificationEventSeverity severity)
        {
            if (messageHash == 0u)
                return;

            if (!_messagesByHash.ContainsKey(messageHash))
            {
                ReportRegisteredMessageMiss(messageHash);
                return;
            }

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(severity);
                return;
            }

            NotificationEventPayload payload = new NotificationEventPayload
            {
                MessageHash = messageHash,
                Severity = (ushort)severity,
                Reserved = 0
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<NotificationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NotificationEventPayload>[8] - deferred notification lane flushed by SystemDispatcher LateUpdate - owner: NotificationEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(NotificationEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<NotificationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NotificationEventPayload>[8] - next-frame notification lane prevents same-frame reentrant dispatch - owner: NotificationEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(NotificationEvents),
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
            ref NativeQueue<NotificationEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
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

            NativeQueue<NotificationEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(
            INotificationEventListener listener,
            in NotificationEventPayload payload)
        {
            try
            {
                listener.OnNotificationEvent(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(INotificationEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= _deferredRegisterListeners.Length)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(INotificationEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= _deferredUnregisterListeners.Length)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(INotificationEventListener listener)
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

        private static void CancelDeferredUnregister(INotificationEventListener listener)
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

        private static bool IsDeferredRegisterPending(INotificationEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(INotificationEventListener listener)
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
                INotificationEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                INotificationEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(INotificationEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportQueueOverflow(NotificationEventSeverity severity)
        {
            _droppedEventCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NotificationQueueOverflowWarningHash,
                NotificationQueueContextHash ^ ((uint)severity << 24),
                Unity.Mathematics.math.max(1, _droppedEventCount));
        }

        private static void ReportRegisteredMessageMiss(uint messageHash)
        {
            _registeredMessageMissCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastRegisteredMessageMissTelemetryFrame == frame)
                return;

            _lastRegisteredMessageMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NotificationRegisteredMessageMissWarningHash,
                NotificationRegisteredMessageContextHash ^ messageHash,
                Unity.Mathematics.math.max(1, _registeredMessageMissCount));
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NotificationListenerOverflowWarningHash,
                NotificationListenerContextHash,
                Unity.Mathematics.math.max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NotificationListenerExceptionWarningHash,
                NotificationListenerExceptionContextHash,
                Unity.Mathematics.math.max(1, _listenerExceptionCount));
        }
    }
}
