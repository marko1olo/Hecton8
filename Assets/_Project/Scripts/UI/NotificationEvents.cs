using System;
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

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct NotificationEventPayload
    {
        [FieldOffset(0)] public uint MessageHash;
        [FieldOffset(4)] public ushort Severity;
        [FieldOffset(6)] public ushort Reserved;
    }

    public interface INotificationEventListener
    {
        void OnNotificationEvent(in NotificationEventPayload payload);
    }

    public static class NotificationEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;
        private const int SpanMessageCapacity = 512;
        private const int SpanMessageCharCapacity = 512;
        private const uint NotificationListenerOverflowWarningHash = 0x4E45564Cu; // NEVL
        private const uint NotificationListenerContextHash = 0x4E455652u; // NEVR
        private const uint NotificationListenerExceptionWarningHash = 0x4E455645u; // NEVE
        private const uint NotificationListenerExceptionContextHash = 0x4E455658u; // NEVX
        private const uint NotificationQueueOverflowWarningHash = 0x4E455651u; // NEVQ
        private const uint NotificationQueueContextHash = 0x4E455650u; // NEVP
        private const uint NotificationRegisteredMessageMissWarningHash = 0x4E45564Du; // NEVM
        private const uint NotificationRegisteredMessageContextHash = 0x4E455643u; // NEVC

        private struct SpanMessageSlot
        {
            public uint MessageHash;
            public int Offset;
            public int Length;
            public byte IsValid;
        }

        private struct NotificationListenerRegistry
        {
            private int _count;
            private INotificationEventListener _slot0;
            private INotificationEventListener _slot1;
            private INotificationEventListener _slot2;
            private INotificationEventListener _slot3;
            private INotificationEventListener _slot4;
            private INotificationEventListener _slot5;
            private INotificationEventListener _slot6;
            private INotificationEventListener _slot7;

            public int Count => _count;

            public void Clear()
            {
                _slot0 = null;
                _slot1 = null;
                _slot2 = null;
                _slot3 = null;
                _slot4 = null;
                _slot5 = null;
                _slot6 = null;
                _slot7 = null;
                _count = 0;
            }

            public bool Contains(INotificationEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(GetAt(i), listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(INotificationEventListener listener)
            {
                if (listener == null || _count >= ListenerCapacity)
                    return false;

                SetAt(_count, listener);
                _count++;
                return true;
            }

            public void Unregister(INotificationEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(GetAt(i), listener))
                        continue;

                    _count--;
                    SetAt(i, GetAt(_count));
                    SetAt(_count, null);
                    return;
                }
            }

            public INotificationEventListener GetAt(int index)
            {
                return index switch
                {
                    0 => _slot0,
                    1 => _slot1,
                    2 => _slot2,
                    3 => _slot3,
                    4 => _slot4,
                    5 => _slot5,
                    6 => _slot6,
                    7 => _slot7,
                    _ => null
                };
            }

            private void SetAt(int index, INotificationEventListener listener)
            {
                switch (index)
                {
                    case 0:
                        _slot0 = listener;
                        break;
                    case 1:
                        _slot1 = listener;
                        break;
                    case 2:
                        _slot2 = listener;
                        break;
                    case 3:
                        _slot3 = listener;
                        break;
                    case 4:
                        _slot4 = listener;
                        break;
                    case 5:
                        _slot5 = listener;
                        break;
                    case 6:
                        _slot6 = listener;
                        break;
                    case 7:
                        _slot7 = listener;
                        break;
                }
            }
        }

        private static NotificationListenerRegistry _listeners;
        private static NotificationListenerRegistry _deferredRegisterListeners;
        private static NotificationListenerRegistry _deferredUnregisterListeners;
        // COLD ALLOC: SpanMessageSlot[512] - notification messages copied from caller-owned char buffers - owner: NotificationEvents
        private static readonly SpanMessageSlot[] _spanMessagesByHash = new SpanMessageSlot[SpanMessageCapacity];
        // COLD ALLOC: char[262144] - span notification backing store - owner: NotificationEvents
        private static readonly char[] _spanMessageCharacters = new char[SpanMessageCapacity * SpanMessageCharCapacity];
        // Fixed inline slots: NotificationEventPayload[8] - deferred notification lane flushed by SystemDispatcher LateUpdate - owner: NotificationEvents
        private static FixedUiEventQueue<NotificationEventPayload> _pendingEvents;
        // Fixed inline slots: NotificationEventPayload[8] - next-frame notification lane prevents same-frame reentrant dispatch - owner: NotificationEvents
        private static FixedUiEventQueue<NotificationEventPayload> _nextFrameEvents;
        private static int _spanMessageCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
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
            _pendingEvents.Clear();
            _nextFrameEvents.Clear();
            _listeners.Clear();
            ClearSpanMessages();
            _deferredRegisterListeners.Clear();
            _deferredUnregisterListeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
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

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
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
                        INotificationEventListener listener = _listeners.GetAt(i);
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

        public static uint ComputeMessageHash(ReadOnlySpan<char> message)
        {
            return IsWhiteSpace(message)
                ? 0u
                : unchecked((uint)LocHash.Compute(message));
        }

        public static uint RegisterMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? 0u
                : RegisterMessage(message.AsSpan());
        }

        public static uint RegisterMessage(ReadOnlySpan<char> message)
        {
            uint messageHash = ComputeMessageHash(message);
            if (messageHash == 0u || message.Length > SpanMessageCharCapacity)
                return 0u;

            if (TryFindSpanMessage(messageHash, out int existingIndex))
            {
                SpanMessageSlot existing = _spanMessagesByHash[existingIndex];
                ReadOnlySpan<char> cached = _spanMessageCharacters.AsSpan(existing.Offset, existing.Length);
                return message.Length == existing.Length && cached.SequenceEqual(message)
                    ? messageHash
                    : 0u;
            }

            if (_spanMessageCount >= _spanMessagesByHash.Length)
                return 0u;

            int storedLength = message.Length;
            int slotIndex = _spanMessageCount++;
            int offset = slotIndex * SpanMessageCharCapacity;
            message.Slice(0, storedLength).CopyTo(_spanMessageCharacters.AsSpan(offset, storedLength));
            SpanMessageSlot slot = default;
            slot.MessageHash = messageHash;
            slot.Offset = offset;
            slot.Length = storedLength;
            slot.IsValid = 1;
            _spanMessagesByHash[slotIndex] = slot;

            return messageHash;
        }

        public static bool TryResolveMessage(uint messageHash, out string message)
        {
            if (TryFindSpanMessage(messageHash, out _))
            {
                message = string.Empty;
                return true;
            }

            message = string.Empty;
            return false;
        }

        public static bool TryResolveMessageSpan(uint messageHash, out ReadOnlySpan<char> message)
        {
            if (TryFindSpanMessage(messageHash, out int index))
            {
                SpanMessageSlot slot = _spanMessagesByHash[index];
                message = _spanMessageCharacters.AsSpan(slot.Offset, slot.Length);
                return true;
            }

            message = ReadOnlySpan<char>.Empty;
            return false;
        }

        [Obsolete("Use TryPushInfo(string) so notification queue refusal stays visible at the producer.", true)]
        public static void PushInfo(string message)
        {
            TryPushInfo(message);
        }

        public static bool TryPushInfo(string message)
        {
            return TryPublish(message, NotificationEventSeverity.Info);
        }

        [Obsolete("Use TryPushInfo(ReadOnlySpan<char>) so notification queue refusal stays visible at the producer.", true)]
        public static void PushInfo(ReadOnlySpan<char> message)
        {
            TryPushInfo(message);
        }

        public static bool TryPushInfo(ReadOnlySpan<char> message)
        {
            return TryPublish(message, NotificationEventSeverity.Info);
        }

        [Obsolete("Use TryPushWarning(string) so notification queue refusal stays visible at the producer.", true)]
        public static void PushWarning(string message)
        {
            TryPushWarning(message);
        }

        public static bool TryPushWarning(string message)
        {
            return TryPublish(message, NotificationEventSeverity.Warning);
        }

        [Obsolete("Use TryPushWarning(ReadOnlySpan<char>) so notification queue refusal stays visible at the producer.", true)]
        public static void PushWarning(ReadOnlySpan<char> message)
        {
            TryPushWarning(message);
        }

        public static bool TryPushWarning(ReadOnlySpan<char> message)
        {
            return TryPublish(message, NotificationEventSeverity.Warning);
        }

        [Obsolete("Use TryPushCritical(string) so notification queue refusal stays visible at the producer.", true)]
        public static void PushCritical(string message)
        {
            TryPushCritical(message);
        }

        public static bool TryPushCritical(string message)
        {
            return TryPublish(message, NotificationEventSeverity.Critical);
        }

        [Obsolete("Use TryPushCritical(ReadOnlySpan<char>) so notification queue refusal stays visible at the producer.", true)]
        public static void PushCritical(ReadOnlySpan<char> message)
        {
            TryPushCritical(message);
        }

        public static bool TryPushCritical(ReadOnlySpan<char> message)
        {
            return TryPublish(message, NotificationEventSeverity.Critical);
        }

        [Obsolete("Use TryPushRegisteredInfo(uint) so notification queue refusal stays visible at the producer.", true)]
        internal static void PushRegisteredInfo(uint messageHash)
        {
            TryPushRegisteredInfo(messageHash);
        }

        internal static bool TryPushRegisteredInfo(uint messageHash)
        {
            return TryPublishRegistered(messageHash, NotificationEventSeverity.Info);
        }

        [Obsolete("Use TryPushRegisteredWarning(uint) so notification queue refusal stays visible at the producer.", true)]
        internal static void PushRegisteredWarning(uint messageHash)
        {
            TryPushRegisteredWarning(messageHash);
        }

        internal static bool TryPushRegisteredWarning(uint messageHash)
        {
            return TryPublishRegistered(messageHash, NotificationEventSeverity.Warning);
        }

        [Obsolete("Use TryPushRegisteredCritical(uint) so notification queue refusal stays visible at the producer.", true)]
        internal static void PushRegisteredCritical(uint messageHash)
        {
            TryPushRegisteredCritical(messageHash);
        }

        internal static bool TryPushRegisteredCritical(uint messageHash)
        {
            return TryPublishRegistered(messageHash, NotificationEventSeverity.Critical);
        }

        private static bool TryPublish(string message, NotificationEventSeverity severity)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return TryPublish(message.AsSpan(), severity);
        }

        private static bool TryPublish(ReadOnlySpan<char> message, NotificationEventSeverity severity)
        {
            uint messageHash = RegisterMessage(message);
            if (messageHash == 0u)
                return false;

            return TryPublishRegistered(messageHash, severity);
        }

        private static bool TryPublishRegistered(uint messageHash, NotificationEventSeverity severity)
        {
            if (messageHash == 0u)
                return false;

            if (!IsMessageRegistered(messageHash))
            {
                ReportRegisteredMessageMiss(messageHash);
                return false;
            }

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(severity);
                return false;
            }

            NotificationEventPayload payload = default;
            payload.MessageHash = messageHash;
            payload.Severity = (ushort)severity;
            payload.Reserved = 0;

            if (_isDispatching)
            {
                if (!_nextFrameEvents.Enqueue(in payload))
                    return false;

                _nextFrameEventCount++;
                return true;
            }

            if (!_pendingEvents.Enqueue(in payload))
                return false;

            _pendingEventCount++;
            return true;
        }

        private static bool IsMessageRegistered(uint messageHash)
        {
            return messageHash != 0u && TryFindSpanMessage(messageHash, out _);
        }

        private static bool TryFindSpanMessage(uint messageHash, out int index)
        {
            for (int i = 0; i < _spanMessageCount; i++)
            {
                SpanMessageSlot slot = _spanMessagesByHash[i];
                if (slot.IsValid != 0 && slot.MessageHash == messageHash)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static void ClearSpanMessages()
        {
            for (int i = 0; i < _spanMessageCount; i++)
                _spanMessagesByHash[i] = default;

            _spanMessageCount = 0;
        }

        private static bool IsWhiteSpace(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents.Configure(PendingEventCapacity);

            if (!_nextFrameEvents.IsCreated)
                _nextFrameEvents.Configure(PendingEventCapacity);
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref FixedUiEventQueue<NotificationEventPayload> queue,
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

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            FixedUiEventQueue<NotificationEventPayload> swap = _pendingEvents;
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
            Hecton8.Core.H8Debug.LogException(exception);
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

            if (!_deferredRegisterListeners.TryRegister(listener))
            {
                ReportListenerRegistrationOverflow();
                return;
            }
        }

        private static void QueueDeferredUnregister(INotificationEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (!_deferredUnregisterListeners.TryRegister(listener))
            {
                ReportListenerRegistrationOverflow();
            }
        }

        private static bool CancelDeferredRegister(INotificationEventListener listener)
        {
            if (!_deferredRegisterListeners.Contains(listener))
                return false;

            _deferredRegisterListeners.Unregister(listener);
            return true;
        }

        private static void CancelDeferredUnregister(INotificationEventListener listener)
        {
            _deferredUnregisterListeners.Unregister(listener);
        }

        private static bool IsDeferredRegisterPending(INotificationEventListener listener)
        {
            return _deferredRegisterListeners.Contains(listener);
        }

        private static bool IsDeferredUnregisterPending(INotificationEventListener listener)
        {
            return _deferredUnregisterListeners.Contains(listener);
        }

        private static void ApplyDeferredListenerMutations()
        {
            int unregisterCount = _deferredUnregisterListeners.Count;
            for (int i = 0; i < unregisterCount; i++)
            {
                INotificationEventListener listener = _deferredUnregisterListeners.GetAt(i);
                if (listener != null)
                    _listeners.Unregister(listener);
            }

            _deferredUnregisterListeners.Clear();

            int registerCount = _deferredRegisterListeners.Count;
            for (int i = 0; i < registerCount; i++)
            {
                INotificationEventListener listener = _deferredRegisterListeners.GetAt(i);
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterListeners.Clear();
        }

        private static void RegisterImmediate(INotificationEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ClearDeferredRegisterListeners()
        {
            _deferredRegisterListeners.Clear();
        }

        private static void ClearDeferredUnregisterListeners()
        {
            _deferredUnregisterListeners.Clear();
        }

        private static void ReportQueueOverflow(NotificationEventSeverity severity)
        {
            _droppedEventCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
