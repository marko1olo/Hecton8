using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    public enum AtlasSignalEventType : byte
    {
        Pulse = 0,
        Detected = 1,
        StrengthChanged = 2,
        Decoded = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AtlasSignalEventPayload
    {
        [FieldOffset(0)] public Vector3 SourcePosition;
        [FieldOffset(12)] public float SignalStrength;
        [FieldOffset(16)] public uint MessageHash;
        [FieldOffset(20)] public ushort EventType;
        [FieldOffset(22)] public ushort Reserved;
        [FieldOffset(24)] private ulong _pad0;
    }

    public interface IAtlasSignalEventListener
    {
        void OnAtlasSignalEvent(in AtlasSignalEventPayload payload);
    }

    public static class AtlasSignalEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;
        private const int DecodedMessageCapacity = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private static readonly uint _QueueOverflowWarningHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.QueueOverflow"));
        private static readonly uint _DuplicateListenerWarningHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.DuplicateListener"));
        private static readonly uint _ListenerRejectedWarningHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.ListenerRejected"));
        private static readonly uint _ListenerExceptionWarningHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.ListenerException"));
        private static readonly uint _UnregisterMissWarningHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.UnregisterMiss"));
        private static readonly uint _DecodedMessageHashCollisionWarningHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.DecodedMessageHashCollision"));
        private static readonly uint _QueueContextHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.PendingQueue"));
        private static readonly uint _ListenerContextHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.Listeners"));
        private static readonly uint _DecodedMessageContextHash = unchecked((uint)LocHash.Compute("AtlasSignalEvents.DecodedMessages"));

        private struct ListenerSlot
        {
            public IAtlasSignalEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct DecodedMessageSlot
        {
            public uint MessageHash;
            public string MessageId;
            public byte IsValid;

            public void Clear()
            {
                MessageHash = 0u;
                MessageId = null;
                IsValid = 0;
            }
        }

        private struct AtlasSignalListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public AtlasSignalListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] - fixed Atlas signal listener slots drained on dispatcher LateUpdate - owner: AtlasSignalEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IAtlasSignalEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IAtlasSignalEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IAtlasSignalEventListener listener)
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

            public IAtlasSignalEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static AtlasSignalListenerRegistry _listeners = new AtlasSignalListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[16] - listener additions deferred while dispatching Atlas signal events - owner: AtlasSignalEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] - listener removals deferred while dispatching Atlas signal events - owner: AtlasSignalEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: DecodedMessageSlot[16] - fixed decoded Atlas message IDs keyed by FNV-1a hash - owner: AtlasSignalEvents
        private static readonly DecodedMessageSlot[] _decodedMessageIdsByHash = new DecodedMessageSlot[DecodedMessageCapacity];
        private static NativeQueue<AtlasSignalEventPayload> _pendingEvents;
        private static NativeQueue<AtlasSignalEventPayload> _nextFrameEvents;
        private static int _decodedMessageCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _duplicateRegistrationCount;
        private static int _listenerRejectCount;
        private static int _listenerExceptionCount;
        private static int _unregisterMissCount;
        private static int _decodedMessageHashCollisionCount;
        private static int _lastOverflowTelemetryFrame = -1;
        private static int _lastDuplicateTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static int _lastUnregisterMissTelemetryFrame = -1;
        private static int _lastDecodedMessageCollisionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DuplicateRegistrationCount => _duplicateRegistrationCount;
        public static int ListenerRejectCount => _listenerRejectCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;
        public static int UnregisterMissCount => _unregisterMissCount;
        public static int DecodedMessageHashCollisionCount => _decodedMessageHashCollisionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AtlasSignalEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AtlasSignalEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            ClearDecodedMessages();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _duplicateRegistrationCount = 0;
            _listenerRejectCount = 0;
            _listenerExceptionCount = 0;
            _unregisterMissCount = 0;
            _decodedMessageHashCollisionCount = 0;
            _lastOverflowTelemetryFrame = -1;
            _lastDuplicateTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _lastUnregisterMissTelemetryFrame = -1;
            _lastDecodedMessageCollisionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IAtlasSignalEventListener listener)
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

        public static void Unregister(IAtlasSignalEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!_listeners.TryUnregister(listener))
                ReportUnregisterMiss();
        }

        private static void RegisterImmediate(IAtlasSignalEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                ReportDuplicateListenerRegistration();
                return;
            }

            if (!_listeners.TryRegister(listener))
                ReportListenerRejected();
        }

        internal static bool IsRegistered(IAtlasSignalEventListener listener)
        {
            return listener != null &&
                (_listeners.Contains(listener) || IsDeferredRegisterPending(listener)) &&
                !IsDeferredUnregisterPending(listener);
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

                if (!_pendingEvents.TryDequeue(out AtlasSignalEventPayload payload))
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
                        IAtlasSignalEventListener listener = _listeners.GetAt(i);
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

        public static uint ComputeMessageHash(string messageId)
        {
            return string.IsNullOrWhiteSpace(messageId)
                ? 0u
                : unchecked((uint)LocHash.Compute(messageId));
        }

        public static bool TryResolveMessageId(uint messageHash, out string messageId)
        {
            return TryResolveDecodedMessage(messageHash, out messageId);
        }

        [Obsolete("Use TryRaisePulse(float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaisePulse(float intensity)
        {
            TryRaisePulse(intensity);
        }

        public static bool TryRaisePulse(float intensity)
        {
            return Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = intensity,
                MessageHash = 0u,
                EventType = (ushort)AtlasSignalEventType.Pulse,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseDetected(Vector3) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseDetected(Vector3 sourcePos)
        {
            TryRaiseDetected(sourcePos);
        }

        public static bool TryRaiseDetected(Vector3 sourcePos)
        {
            return Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = sourcePos,
                SignalStrength = 0f,
                MessageHash = 0u,
                EventType = (ushort)AtlasSignalEventType.Detected,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseStrengthChanged(float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseStrengthChanged(float strength)
        {
            TryRaiseStrengthChanged(strength);
        }

        public static bool TryRaiseStrengthChanged(float strength)
        {
            return Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = strength,
                MessageHash = 0u,
                EventType = (ushort)AtlasSignalEventType.StrengthChanged,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseDecoded(uint messageHash). String ingress is not allowed on first-party event lanes.", true)]
        public static void RaiseDecoded(string messageId)
        {
            TryRaiseDecodedFromString(messageId);
        }

        private static bool TryRaiseDecodedFromString(string messageId)
        {
            uint messageHash = ComputeMessageHash(messageId);
            if (messageHash == 0u)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow((ushort)AtlasSignalEventType.Decoded);
                return false;
            }

            if (!TryRegisterDecodedMessage(messageHash, messageId, out bool hashCollision))
            {
                ReportQueueOverflow((ushort)AtlasSignalEventType.Decoded);
                return false;
            }

            if (hashCollision)
                ReportDecodedMessageHashCollision(messageHash);

            return TryRaiseDecoded(messageHash);
        }

        [Obsolete("Use TryRaiseDecoded(uint messageHash) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool RaiseDecoded(uint messageHash)
        {
            return TryRaiseDecoded(messageHash);
        }

        public static bool TryRaiseDecoded(uint messageHash)
        {
            if (messageHash == 0u)
                return false;

            return Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = 0f,
                MessageHash = messageHash,
                EventType = (ushort)AtlasSignalEventType.Decoded,
                Reserved = 0
            });
        }

        private static bool TryRegisterDecodedMessage(uint messageHash, string messageId, out bool hashCollision)
        {
            hashCollision = false;
            if (messageHash == 0u)
                return false;

            if (TryFindDecodedMessage(messageHash, out int existingIndex))
            {
                string existingMessageId = _decodedMessageIdsByHash[existingIndex].MessageId;
                hashCollision = !string.Equals(existingMessageId, messageId, StringComparison.Ordinal);
                return true;
            }

            if (_decodedMessageCount >= _decodedMessageIdsByHash.Length)
                return false;

            _decodedMessageIdsByHash[_decodedMessageCount++] = new DecodedMessageSlot
            {
                MessageHash = messageHash,
                MessageId = messageId,
                IsValid = 1
            };
            return true;
        }

        private static bool TryResolveDecodedMessage(uint messageHash, out string messageId)
        {
            if (TryFindDecodedMessage(messageHash, out int index))
            {
                messageId = _decodedMessageIdsByHash[index].MessageId ?? string.Empty;
                return true;
            }

            messageId = string.Empty;
            return false;
        }

        private static bool TryFindDecodedMessage(uint messageHash, out int index)
        {
            for (int i = 0; i < _decodedMessageCount; i++)
            {
                DecodedMessageSlot slot = _decodedMessageIdsByHash[i];
                if (slot.IsValid != 0 && slot.MessageHash == messageHash)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static void ClearDecodedMessages()
        {
            for (int i = 0; i < _decodedMessageCount; i++)
                _decodedMessageIdsByHash[i].Clear();

            _decodedMessageCount = 0;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AtlasSignalEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<AtlasSignalEventPayload>[16] - deferred Atlas signal lane flushed by SystemDispatcher LateUpdate - owner: AtlasSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(AtlasSignalEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<AtlasSignalEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<AtlasSignalEventPayload>[16] - next-frame Atlas signal lane prevents same-frame reentrant dispatch - owner: AtlasSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(AtlasSignalEvents),
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

        private static bool Enqueue(in AtlasSignalEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(payload.EventType);
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
            ref NativeQueue<AtlasSignalEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

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
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<AtlasSignalEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IAtlasSignalEventListener listener, in AtlasSignalEventPayload payload)
        {
            try
            {
                listener.OnAtlasSignalEvent(in payload);
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

        private static void QueueDeferredRegister(IAtlasSignalEventListener listener)
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
                ReportListenerRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IAtlasSignalEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IAtlasSignalEventListener listener)
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

        private static void CancelDeferredUnregister(IAtlasSignalEventListener listener)
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

        private static bool IsDeferredRegisterPending(IAtlasSignalEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IAtlasSignalEventListener listener)
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
                IAtlasSignalEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null && !_listeners.TryUnregister(listener))
                    ReportUnregisterMiss();
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IAtlasSignalEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void ReportQueueOverflow(ushort eventType)
        {
            _droppedEventCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastOverflowTelemetryFrame == frame)
                return;

            _lastOverflowTelemetryFrame = frame;
            uint contextHash = _QueueContextHash ^ ((uint)eventType << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _QueueOverflowWarningHash,
                contextHash,
                _droppedEventCount);
        }

        private static void ReportDuplicateListenerRegistration()
        {
            _duplicateRegistrationCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDuplicateTelemetryFrame == frame)
                return;

            _lastDuplicateTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DuplicateListenerWarningHash,
                _ListenerContextHash,
                _duplicateRegistrationCount);
        }

        private static void ReportListenerRejected()
        {
            _listenerRejectCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerRejectedWarningHash,
                _ListenerContextHash,
                _listenerRejectCount);
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerExceptionWarningHash,
                _ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static void ReportUnregisterMiss()
        {
            _unregisterMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastUnregisterMissTelemetryFrame == frame)
                return;

            _lastUnregisterMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _UnregisterMissWarningHash,
                _ListenerContextHash,
                _unregisterMissCount);
        }

        private static void ReportDecodedMessageHashCollision(uint messageHash)
        {
            _decodedMessageHashCollisionCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDecodedMessageCollisionTelemetryFrame == frame)
                return;

            _lastDecodedMessageCollisionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DecodedMessageHashCollisionWarningHash,
                _DecodedMessageContextHash ^ messageHash,
                _decodedMessageHashCollisionCount);
        }
    }
}
