using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Interaction;
using Unity.Collections;

namespace Hecton8.Core
{
    public enum NarrativeEventType : byte
    {
        DiscoveryMade = 0,
        DepthTierReached = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NarrativeEventPayload
    {
        public uint DiscoveryHash;
        public ushort EventType;
        public short DepthTier;
    }

    public interface INarrativeEventListener
    {
        void OnNarrativeEvent(in NarrativeEventPayload payload);
    }

    public interface INarrativePointOfInterestListener
    {
        void OnNarrativePointOfInterestRegistered(NarrativeDiscovery poi);
        void OnNarrativePointOfInterestDisposed(NarrativeDiscovery poi);
    }

    public static class NarrativeEvents
    {
        // COLD ALLOC: RegistryBucket<INarrativeEventListener>[16] - narrative event listener registry drained on dispatcher LateUpdate - owner: NarrativeEvents
        private static readonly RegistryBucket<INarrativeEventListener> _listeners = new RegistryBucket<INarrativeEventListener>(16);
        // COLD ALLOC: RegistryBucket<INarrativePointOfInterestListener>[8] - narrative POI listener registry for direct world authoring callbacks - owner: NarrativeEvents
        private static readonly RegistryBucket<INarrativePointOfInterestListener> _pointOfInterestListeners = new RegistryBucket<INarrativePointOfInterestListener>(8);
        // COLD ALLOC: Dictionary<uint,string>[64] - hashed narrative discovery id lookup for queue listeners that still persist authored ids - owner: NarrativeEvents
        private static readonly Dictionary<uint, string> _discoveryIdsByHash = new Dictionary<uint, string>(64);
        private static NativeQueue<NarrativeEventPayload> _pendingEvents;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _pointOfInterestListeners.Clear();
            _discoveryIdsByHash.Clear();
        }

        public static void Register(INarrativeEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(INarrativeEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void RegisterPointOfInterestListener(INarrativePointOfInterestListener listener)
        {
            if (listener == null)
                return;

            _pointOfInterestListeners.Register(listener);
        }

        public static void UnregisterPointOfInterestListener(INarrativePointOfInterestListener listener)
        {
            if (listener == null)
                return;

            _pointOfInterestListeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (_pendingEvents.TryDequeue(out NarrativeEventPayload payload))
            {
                INarrativeEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnNarrativeEvent(in payload);
            }
        }

        public static uint ComputeDiscoveryHash(string discoveryId)
        {
            return string.IsNullOrWhiteSpace(discoveryId)
                ? 0u
                : unchecked((uint)LocHash.Compute(discoveryId));
        }

        public static bool TryResolveDiscoveryId(uint discoveryHash, out string discoveryId)
        {
            return _discoveryIdsByHash.TryGetValue(discoveryHash, out discoveryId);
        }

        public static void RaiseNarrativePOIRegistered(NarrativeDiscovery poi)
        {
            if (poi == null || _pointOfInterestListeners.Count <= 0)
                return;

            INarrativePointOfInterestListener[] rawArray = _pointOfInterestListeners.RawArray;
            int count = _pointOfInterestListeners.Count;
            for (int i = count - 1; i >= 0; i--)
                rawArray[i].OnNarrativePointOfInterestRegistered(poi);
        }

        public static void RaiseNarrativePOIDisposed(NarrativeDiscovery poi)
        {
            if (poi == null || _pointOfInterestListeners.Count <= 0)
                return;

            INarrativePointOfInterestListener[] rawArray = _pointOfInterestListeners.RawArray;
            int count = _pointOfInterestListeners.Count;
            for (int i = count - 1; i >= 0; i--)
                rawArray[i].OnNarrativePointOfInterestDisposed(poi);
        }

        public static void RaiseDiscoveryMade(string discoveryId)
        {
            uint discoveryHash = ComputeDiscoveryHash(discoveryId);
            if (discoveryHash == 0u)
                return;

            if (!_discoveryIdsByHash.ContainsKey(discoveryHash))
                _discoveryIdsByHash.Add(discoveryHash, discoveryId);

            EnsureInitialized();
            _pendingEvents.Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = discoveryHash,
                EventType = (ushort)NarrativeEventType.DiscoveryMade,
                DepthTier = 0
            });
        }

        public static void RaiseDepthTierReached(int tier)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = 0u,
                EventType = (ushort)NarrativeEventType.DepthTierReached,
                DepthTier = (short)tier
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<NarrativeEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NarrativeEventPayload>[16] - deferred narrative event lane flushed by SystemDispatcher LateUpdate - owner: NarrativeEvents
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }
}
