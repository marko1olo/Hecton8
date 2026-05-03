using Hecton8.Core;
using Unity.Collections;

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

        private struct RelayEventPayload
        {
            public ulong RelayEntityId;
            public byte FirstActivation;
        }

        private static readonly RegistryBucket<IEmergencyServiceRelayEventListener> _listeners = new RegistryBucket<IEmergencyServiceRelayEventListener>(16);
        private static readonly System.Collections.Generic.Dictionary<ulong, EmergencyServiceRelay> _relaysByInstanceId = new System.Collections.Generic.Dictionary<ulong, EmergencyServiceRelay>(32);
        private static NativeQueue<RelayEventPayload> _pendingEvents;
        private static NativeQueue<RelayEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EmergencyServiceRelayEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EmergencyServiceRelayEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _listeners.Clear();
            _relaysByInstanceId.Clear();
        }

        public static void Register(IEmergencyServiceRelayEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IEmergencyServiceRelayEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Raises the relay activation event.
        /// </summary>
        /// <param name="relay">Relay that was accessed.</param>
        /// <param name="firstActivation">True when this was the first discovery-grade access.</param>
        public static void RaiseRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            if (relay == null || _listeners.Count <= 0 || _pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            EnsureInitialized();
            ulong relayEntityId = UnityEngine.EntityId.ToULong(relay.GetEntityId());
            _relaysByInstanceId[relayEntityId] = relay;
            RelayEventPayload payload = new RelayEventPayload
            {
                RelayEntityId = relayEntityId,
                FirstActivation = firstActivation ? (byte)1 : (byte)0
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

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out RelayEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                if (!_relaysByInstanceId.TryGetValue(payload.RelayEntityId, out EmergencyServiceRelay relay) || relay == null)
                    continue;

                IEmergencyServiceRelayEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                bool firstActivation = payload.FirstActivation != 0;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IEmergencyServiceRelayEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnEmergencyServiceRelayActivated(relay, firstActivation);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<RelayEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RelayEventPayload>[16] - emergency relay event lane flushed by SystemDispatcher - owner: EmergencyServiceRelayEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(EmergencyServiceRelayEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<RelayEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RelayEventPayload>[16] - next-frame emergency relay event lane prevents same-frame reentrant dispatch - owner: EmergencyServiceRelayEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(EmergencyServiceRelayEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
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

            NativeQueue<RelayEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
