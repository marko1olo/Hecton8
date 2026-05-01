using System;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.World
{
    /// <summary>
    /// Static event bus for emergency service relay interactions.
    /// </summary>
    public static class EmergencyServiceRelayEvents
    {
        private struct RelayEventPayload
        {
            public int RelayInstanceId;
            public byte FirstActivation;
        }

        private static readonly RegistryBucket<Action<EmergencyServiceRelay, bool>> _relayActivatedListeners = new RegistryBucket<Action<EmergencyServiceRelay, bool>>(16);
        private static readonly System.Collections.Generic.Dictionary<int, EmergencyServiceRelay> _relaysByInstanceId = new System.Collections.Generic.Dictionary<int, EmergencyServiceRelay>(32);
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

            _relayActivatedListeners.Clear();
            _relaysByInstanceId.Clear();
        }

        public static void RegisterRelayActivated(Action<EmergencyServiceRelay, bool> listener)
        {
            if (listener != null && !_relayActivatedListeners.Contains(listener))
                _relayActivatedListeners.Register(listener);
        }

        public static void UnregisterRelayActivated(Action<EmergencyServiceRelay, bool> listener)
        {
            if (listener != null && _relayActivatedListeners.Contains(listener))
                _relayActivatedListeners.Unregister(listener);
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
            int instanceId = relay.GetInstanceID();
            _relaysByInstanceId[instanceId] = relay;
            _pendingEvents.Enqueue(new RelayEventPayload
            {
                RelayInstanceId = instanceId,
                FirstActivation = firstActivation ? (byte)1 : (byte)0
            });
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out RelayEventPayload payload))
            {
                if (!_relaysByInstanceId.TryGetValue(payload.RelayInstanceId, out EmergencyServiceRelay relay) || relay == null)
                    continue;

                Action<EmergencyServiceRelay, bool>[] rawArray = _relayActivatedListeners.RawArray;
                int count = _relayActivatedListeners.Count;
                bool firstActivation = payload.FirstActivation != 0;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i]?.Invoke(relay, firstActivation);
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents = new NativeQueue<RelayEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RelayEventPayload>[16] - emergency relay event lane flushed by SystemDispatcher - owner: EmergencyServiceRelayEvents
        }
    }
}
