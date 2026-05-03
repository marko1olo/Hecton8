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
        private const int PendingEventCapacity = 16;

        // COLD ALLOC: RegistryBucket<INarrativeEventListener>[16] - narrative event listener registry drained on dispatcher LateUpdate - owner: NarrativeEvents
        private static readonly RegistryBucket<INarrativeEventListener> _listeners = new RegistryBucket<INarrativeEventListener>(16);
        // COLD ALLOC: RegistryBucket<INarrativePointOfInterestListener>[8] - narrative POI listener registry for direct world authoring callbacks - owner: NarrativeEvents
        private static readonly RegistryBucket<INarrativePointOfInterestListener> _pointOfInterestListeners = new RegistryBucket<INarrativePointOfInterestListener>(8);
        // COLD ALLOC: Dictionary<uint,string>[64] - hashed narrative discovery id lookup for queue listeners that still persist authored ids - owner: NarrativeEvents
        private static readonly Dictionary<uint, string> _discoveryIdsByHash = new Dictionary<uint, string>(64);
        private static NativeQueue<NarrativeEventPayload> _pendingEvents;
        private static NativeQueue<NarrativeEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(NarrativeEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(NarrativeEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pointOfInterestListeners.Clear();
            _discoveryIdsByHash.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
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

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out NarrativeEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                INarrativeEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                        rawArray[i].OnNarrativeEvent(in payload);
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

            if (!Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = discoveryHash,
                EventType = (ushort)NarrativeEventType.DiscoveryMade,
                DepthTier = 0
            }))
            {
                return;
            }

            if (!_discoveryIdsByHash.ContainsKey(discoveryHash))
                _discoveryIdsByHash.Add(discoveryHash, discoveryId);
        }

        public static void RaiseDepthTierReached(int tier)
        {
            Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = 0u,
                EventType = (ushort)NarrativeEventType.DepthTierReached,
                DepthTier = (short)tier
            });
        }

        private static bool Enqueue(in NarrativeEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<NarrativeEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NarrativeEventPayload>[16] - deferred narrative event lane flushed by SystemDispatcher LateUpdate - owner: NarrativeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(NarrativeEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<NarrativeEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NarrativeEventPayload>[16] - next-frame narrative event lane prevents same-frame reentrant dispatch - owner: NarrativeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(NarrativeEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
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
            ref NativeQueue<NarrativeEventPayload> queue,
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

            NativeQueue<NarrativeEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
