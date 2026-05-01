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
        private struct RelayEventPayload
        {
            public ulong RelayEntityId;
            public byte FirstActivation;
        }

        private static readonly RegistryBucket<IEmergencyServiceRelayEventListener> _listeners = new RegistryBucket<IEmergencyServiceRelayEventListener>(16);
        private static readonly System.Collections.Generic.Dictionary<ulong, EmergencyServiceRelay> _relaysByInstanceId = new System.Collections.Generic.Dictionary<ulong, EmergencyServiceRelay>(32);
        private static NativeQueue<RelayEventPayload> _pendingEvents;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEvents.Count : 0;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

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
            if (relay == null)
                return;

            EnsureInitialized();
            ulong relayEntityId = UnityEngine.EntityId.ToULong(relay.GetEntityId());
            _relaysByInstanceId[relayEntityId] = relay;
            _pendingEvents.Enqueue(new RelayEventPayload
            {
                RelayEntityId = relayEntityId,
                FirstActivation = firstActivation ? (byte)1 : (byte)0
            });
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out RelayEventPayload payload))
            {
                if (!_relaysByInstanceId.TryGetValue(payload.RelayEntityId, out EmergencyServiceRelay relay) || relay == null)
                    continue;

                IEmergencyServiceRelayEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                bool firstActivation = payload.FirstActivation != 0;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnEmergencyServiceRelayActivated(relay, firstActivation);
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents = new NativeQueue<RelayEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RelayEventPayload>[16] - emergency relay event lane flushed by SystemDispatcher - owner: EmergencyServiceRelayEvents
        }
    }
}
