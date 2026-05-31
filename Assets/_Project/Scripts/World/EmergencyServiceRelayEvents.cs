using System;
using Hecton8.Core;

namespace Hecton8.World
{
    /// <summary>
    /// Listener contract for queue-backed emergency relay notifications.
    /// </summary>
    public interface IEmergencyServiceRelayEventListener
    {
        /// <summary>Called when an emergency relay is activated.</summary>
        /// <param name="relay">Activated relay.</param>
        /// <param name="firstActivation">True on first discovery-grade activation.</param>
        void OnEmergencyServiceRelayActivated(EmergencyServiceRelay relay, bool firstActivation);
    }

    /// <summary>
    /// Static event bus for emergency service relay interactions.
    /// </summary>
    public static class EmergencyServiceRelayEvents
    {
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 16;
        private const int RelaySidecarCapacity = 32;
        private const uint ListenerRejectedWarningHash = 0x4552524Au;
        private const uint ListenerExceptionWarningHash = 0x45524558u;
        private const uint ListenerContextHash = 0x45524C53u;

        private struct RelayEventPayload
        {
            public ulong RelayEntityId;
            public byte FirstActivation;
        }

        private struct ListenerSlot
        {
            public IEmergencyServiceRelayEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct RelaySlot
        {
            public ulong RelayEntityId;
            public EmergencyServiceRelay Relay;

            public void Clear()
            {
                RelayEntityId = 0UL;
                Relay = null;
            }
        }

        private struct EmergencyRelayListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public EmergencyRelayListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IEmergencyServiceRelayEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IEmergencyServiceRelayEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IEmergencyServiceRelayEventListener listener)
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

            public IEmergencyServiceRelayEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static EmergencyRelayListenerRegistry _listeners = new EmergencyRelayListenerRegistry(ListenerCapacity);
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static readonly RelaySlot[] _relaySlots = new RelaySlot[RelaySidecarCapacity];
        // COLD ALLOC: RelayEventPayload[16] - bounded relay event ring flushed by SystemDispatcher - owner: EmergencyServiceRelayEvents
        private static RelayEventPayload[] _pendingEvents = new RelayEventPayload[PendingEventCapacity];
        // COLD ALLOC: RelayEventPayload[16] - bounded next-frame relay ring for reentrant dispatch - owner: EmergencyServiceRelayEvents
        private static RelayEventPayload[] _nextFrameEvents = new RelayEventPayload[PendingEventCapacity];
        private static int _relaySlotCount;
        private static int _pendingEventHead;
        private static int _pendingEventTail;
        private static int _pendingEventCount;
        private static int _nextFrameEventHead;
        private static int _nextFrameEventTail;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingEventHead = 0;
            _pendingEventTail = 0;
            _pendingEventCount = 0;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _isDispatching = false;
            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterListeners.Length);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterListeners.Length);
            ClearRelaySlots();
        }

        public static void Register(IEmergencyServiceRelayEventListener listener)
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

        public static void Unregister(IEmergencyServiceRelayEventListener listener)
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
        /// Raises the relay activation event.
        /// </summary>
        /// <param name="relay">Relay that was accessed.</param>
        /// <param name="firstActivation">True when this was the first discovery-grade access.</param>
        public static bool TryRaiseRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            if (relay == null || _listeners.Count <= 0 || _pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            ulong relayEntityId = UnityEngine.EntityId.ToULong(relay.GetEntityId());
            if (!TryStoreRelay(relayEntityId, relay))
                return false;

            RelayEventPayload payload = new RelayEventPayload
            {
                RelayEntityId = relayEntityId,
                FirstActivation = firstActivation ? (byte)1 : (byte)0
            };

            if (_isDispatching)
            {
                return TryEnqueueNextFrame(payload);
            }

            return TryEnqueuePending(payload);
        }

        [System.Obsolete("Use TryRaiseRelayActivated so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
            => TryRaiseRelayActivated(relay, firstActivation);

        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
            {
                DropQueuedEvents();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            if (_pendingEventCount <= 0)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && _pendingEventCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!TryDequeuePending(out RelayEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (!TryResolveRelay(payload.RelayEntityId, out EmergencyServiceRelay relay) || relay == null)
                    continue;

                int count = _listeners.Count;
                bool firstActivation = payload.FirstActivation != 0;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IEmergencyServiceRelayEventListener listener = _listeners.GetAt(i);
                        if (listener != null && !IsDeferredUnregisterPending(listener))
                            DispatchToListener(listener, relay, firstActivation);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }

            if (_pendingEventCount <= 0)
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
                PruneRelayReferencesIfIdle();
            }
        }

        private static bool TryEnqueuePending(RelayEventPayload payload)
        {
            if (_pendingEventCount >= PendingEventCapacity)
                return false;

            _pendingEvents[_pendingEventTail] = payload;
            _pendingEventTail = (_pendingEventTail + 1) % PendingEventCapacity;
            _pendingEventCount++;
            return true;
        }

        private static bool TryEnqueueNextFrame(RelayEventPayload payload)
        {
            if (_nextFrameEventCount >= PendingEventCapacity)
                return false;

            _nextFrameEvents[_nextFrameEventTail] = payload;
            _nextFrameEventTail = (_nextFrameEventTail + 1) % PendingEventCapacity;
            _nextFrameEventCount++;
            return true;
        }

        private static bool TryDequeuePending(out RelayEventPayload payload)
        {
            if (_pendingEventCount <= 0)
            {
                payload = default;
                return false;
            }

            payload = _pendingEvents[_pendingEventHead];
            _pendingEvents[_pendingEventHead] = default;
            _pendingEventHead = (_pendingEventHead + 1) % PendingEventCapacity;
            _pendingEventCount--;
            return true;
        }

        private static void DispatchToListener(IEmergencyServiceRelayEventListener listener, EmergencyServiceRelay relay, bool firstActivation)
        {
            listener.OnEmergencyServiceRelayActivated(relay, firstActivation);
        }

        private static void QueueDeferredRegister(IEmergencyServiceRelayEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            CancelDeferredUnregister(listener);
            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IEmergencyServiceRelayEventListener listener)
        {
            CancelDeferredRegister(listener);
            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool IsDeferredRegisterPending(IEmergencyServiceRelayEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IEmergencyServiceRelayEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void CancelDeferredRegister(IEmergencyServiceRelayEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                int tail = _deferredRegisterCount - i - 1;
                if (tail > 0)
                    Array.Copy(_deferredRegisterListeners, i + 1, _deferredRegisterListeners, i, tail);

                _deferredRegisterListeners[--_deferredRegisterCount].Clear();
                return;
            }
        }

        private static void CancelDeferredUnregister(IEmergencyServiceRelayEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                int tail = _deferredUnregisterCount - i - 1;
                if (tail > 0)
                    Array.Copy(_deferredUnregisterListeners, i + 1, _deferredUnregisterListeners, i, tail);

                _deferredUnregisterListeners[--_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IEmergencyServiceRelayEventListener listener = _deferredUnregisterListeners[i].Listener;
                if (listener != null)
                    _listeners.TryUnregister(listener);

                _deferredUnregisterListeners[i].Clear();
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                RegisterImmediate(_deferredRegisterListeners[i].Listener);
                _deferredRegisterListeners[i].Clear();
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IEmergencyServiceRelayEventListener listener)
        {
            if (listener == null || _listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerRejectedWarningHash,
                ListenerContextHash,
                UnityEngine.Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void DropQueuedEvents()
        {
            for (int i = 0; i < _pendingEventCount; i++)
            {
                int index = (_pendingEventHead + i) % PendingEventCapacity;
                _pendingEvents[index] = default;
            }

            for (int i = 0; i < _nextFrameEventCount; i++)
            {
                int index = (_nextFrameEventHead + i) % PendingEventCapacity;
                _nextFrameEvents[index] = default;
            }

            _pendingEventHead = 0;
            _pendingEventTail = 0;
            _pendingEventCount = 0;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
            ClearRelaySlots();
        }

        private static void PruneRelayReferencesIfIdle()
        {
            if (_pendingEventCount + _nextFrameEventCount <= 0)
                ClearRelaySlots();
        }

        private static bool TryStoreRelay(ulong relayEntityId, EmergencyServiceRelay relay)
        {
            for (int i = 0; i < _relaySlotCount; i++)
            {
                if (_relaySlots[i].RelayEntityId != relayEntityId)
                    continue;

                _relaySlots[i].Relay = relay;
                return true;
            }

            if (_relaySlotCount >= RelaySidecarCapacity)
                return false;

            _relaySlots[_relaySlotCount++] = new RelaySlot
            {
                RelayEntityId = relayEntityId,
                Relay = relay
            };
            return true;
        }

        private static bool TryResolveRelay(ulong relayEntityId, out EmergencyServiceRelay relay)
        {
            for (int i = 0; i < _relaySlotCount; i++)
            {
                if (_relaySlots[i].RelayEntityId != relayEntityId)
                    continue;

                relay = _relaySlots[i].Relay;
                return relay != null;
            }

            relay = null;
            return false;
        }

        private static void ClearRelaySlots()
        {
            for (int i = 0; i < _relaySlotCount; i++)
                _relaySlots[i].Clear();

            _relaySlotCount = 0;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (_pendingEventCount > 0 || _nextFrameEventCount <= 0)
            {
                return;
            }

            RelayEventPayload[] swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventHead = _nextFrameEventHead;
            _pendingEventTail = _nextFrameEventTail;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
        }
    }
}
