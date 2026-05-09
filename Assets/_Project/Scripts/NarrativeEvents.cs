using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Interaction;
using Unity.Collections;

namespace Hecton8.Core
{
    public enum NarrativeEventType : byte
    {
        DiscoveryMade = 0,
        DepthTierReached = 1,
        AudioLogFound = 2
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
        private const int ListenerCapacity = 16;
        private const int PointOfInterestListenerCapacity = 8;
        private const int PendingEventCapacity = 16;
        private const uint NarrativeListenerOverflowWarningHash = 0x4E41564Cu; // NAVL
        private const uint NarrativeListenerContextHash = 0x4E415652u; // NAVR
        private const uint NarrativeListenerExceptionWarningHash = 0x4E415645u; // NAVE
        private const uint NarrativeListenerExceptionContextHash = 0x4E415658u; // NAVX
        private const uint NarrativeQueueOverflowWarningHash = 0x4E415651u; // NAVQ
        private const uint NarrativeQueueContextHash = 0x4E415650u; // NAVP

        // COLD ALLOC: RegistryBucket<INarrativeEventListener>[16] - narrative event listener registry drained on dispatcher LateUpdate - owner: NarrativeEvents
        private static readonly RegistryBucket<INarrativeEventListener> _listeners = new RegistryBucket<INarrativeEventListener>(ListenerCapacity);
        // COLD ALLOC: INarrativeEventListener[16] - listener additions deferred while dispatching narrative events - owner: NarrativeEvents
        private static readonly INarrativeEventListener[] _deferredRegisterListeners = new INarrativeEventListener[ListenerCapacity];
        // COLD ALLOC: INarrativeEventListener[16] - listener removals deferred while dispatching narrative events - owner: NarrativeEvents
        private static readonly INarrativeEventListener[] _deferredUnregisterListeners = new INarrativeEventListener[ListenerCapacity];
        // COLD ALLOC: RegistryBucket<INarrativePointOfInterestListener>[8] - narrative POI listener registry for direct world authoring callbacks - owner: NarrativeEvents
        private static readonly RegistryBucket<INarrativePointOfInterestListener> _pointOfInterestListeners = new RegistryBucket<INarrativePointOfInterestListener>(PointOfInterestListenerCapacity);
        // COLD ALLOC: INarrativePointOfInterestListener[8] - POI listener additions deferred while dispatching direct callbacks - owner: NarrativeEvents
        private static readonly INarrativePointOfInterestListener[] _deferredPoiRegisterListeners = new INarrativePointOfInterestListener[PointOfInterestListenerCapacity];
        // COLD ALLOC: INarrativePointOfInterestListener[8] - POI listener removals deferred while dispatching direct callbacks - owner: NarrativeEvents
        private static readonly INarrativePointOfInterestListener[] _deferredPoiUnregisterListeners = new INarrativePointOfInterestListener[PointOfInterestListenerCapacity];
        // COLD ALLOC: Dictionary<uint,string>[64] - hashed narrative discovery id lookup for queue listeners that still persist authored ids - owner: NarrativeEvents
        private static readonly Dictionary<uint, string> _discoveryIdsByHash = new Dictionary<uint, string>(64);
        private static NativeQueue<NarrativeEventPayload> _pendingEvents;
        private static NativeQueue<NarrativeEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _deferredPoiRegisterCount;
        private static int _deferredPoiUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;
        private static bool _isDispatchingPointOfInterest;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

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
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            Array.Clear(_deferredPoiRegisterListeners, 0, _deferredPoiRegisterCount);
            Array.Clear(_deferredPoiUnregisterListeners, 0, _deferredPoiUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _deferredPoiRegisterCount = 0;
            _deferredPoiUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
            _isDispatchingPointOfInterest = false;
        }

        public static void Register(INarrativeEventListener listener)
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

        public static void Unregister(INarrativeEventListener listener)
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

        public static void RegisterPointOfInterestListener(INarrativePointOfInterestListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatchingPointOfInterest)
            {
                QueueDeferredPointOfInterestRegister(listener);
                return;
            }

            RegisterPointOfInterestImmediate(listener);
        }

        public static void UnregisterPointOfInterestListener(INarrativePointOfInterestListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatchingPointOfInterest)
            {
                QueueDeferredPointOfInterestUnregister(listener);
                return;
            }

            if (_pointOfInterestListeners.Contains(listener))
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
                    {
                        INarrativeEventListener listener = rawArray[i];
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
            _isDispatchingPointOfInterest = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    INarrativePointOfInterestListener listener = rawArray[i];
                    if (listener == null || IsDeferredPointOfInterestUnregisterPending(listener))
                        continue;

                    try
                    {
                        listener.OnNarrativePointOfInterestRegistered(poi);
                    }
                    catch (Exception exception)
                    {
                        ReportListenerDispatchException();
                        LogListenerDispatchException(exception);
                    }
                }
            }
            finally
            {
                _isDispatchingPointOfInterest = false;
                ApplyDeferredPointOfInterestListenerMutations();
            }
        }

        public static void RaiseNarrativePOIDisposed(NarrativeDiscovery poi)
        {
            if (poi == null || _pointOfInterestListeners.Count <= 0)
                return;

            INarrativePointOfInterestListener[] rawArray = _pointOfInterestListeners.RawArray;
            int count = _pointOfInterestListeners.Count;
            _isDispatchingPointOfInterest = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    INarrativePointOfInterestListener listener = rawArray[i];
                    if (listener == null || IsDeferredPointOfInterestUnregisterPending(listener))
                        continue;

                    try
                    {
                        listener.OnNarrativePointOfInterestDisposed(poi);
                    }
                    catch (Exception exception)
                    {
                        ReportListenerDispatchException();
                        LogListenerDispatchException(exception);
                    }
                }
            }
            finally
            {
                _isDispatchingPointOfInterest = false;
                ApplyDeferredPointOfInterestListenerMutations();
            }
        }

        public static void RaiseDiscoveryMade(string discoveryId)
        {
            uint discoveryHash = ComputeDiscoveryHash(discoveryId);
            if (discoveryHash == 0u)
                return;

            if (!RaiseDiscoveryMade(discoveryHash))
                return;

            if (!_discoveryIdsByHash.ContainsKey(discoveryHash))
                _discoveryIdsByHash.Add(discoveryHash, discoveryId);
        }

        public static bool RaiseDiscoveryMade(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return false;

            return Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = discoveryHash,
                EventType = (ushort)NarrativeEventType.DiscoveryMade,
                DepthTier = 0
            });
        }

        public static void RaiseAudioLogFound(string logId)
        {
            uint logHash = ComputeDiscoveryHash(logId);
            if (logHash == 0u)
                return;

            if (!RaiseAudioLogFound(logHash))
                return;

            if (!_discoveryIdsByHash.ContainsKey(logHash))
                _discoveryIdsByHash.Add(logHash, logId);
        }

        public static bool RaiseAudioLogFound(uint logHash)
        {
            if (logHash == 0u)
                return false;

            return Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = logHash,
                EventType = (ushort)NarrativeEventType.AudioLogFound,
                DepthTier = 0
            });
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
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
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

        private static void DispatchToListener(
            INarrativeEventListener listener,
            in NarrativeEventPayload payload)
        {
            try
            {
                listener.OnNarrativeEvent(in payload);
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

        private static void QueueDeferredRegister(INarrativeEventListener listener)
        {
            if (RemoveDeferredListener(_deferredUnregisterListeners, ref _deferredUnregisterCount, listener))
                return;

            if (_listeners.Contains(listener) ||
                ContainsDeferredListener(_deferredRegisterListeners, _deferredRegisterCount, listener))
            {
                return;
            }

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(INarrativeEventListener listener)
        {
            if (RemoveDeferredListener(_deferredRegisterListeners, ref _deferredRegisterCount, listener))
                return;

            if (!_listeners.Contains(listener) ||
                ContainsDeferredListener(_deferredUnregisterListeners, _deferredUnregisterCount, listener))
            {
                return;
            }

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool IsDeferredUnregisterPending(INarrativeEventListener listener)
        {
            return ContainsDeferredListener(_deferredUnregisterListeners, _deferredUnregisterCount, listener);
        }

        private static void QueueDeferredPointOfInterestRegister(INarrativePointOfInterestListener listener)
        {
            if (RemoveDeferredListener(_deferredPoiUnregisterListeners, ref _deferredPoiUnregisterCount, listener))
                return;

            if (_pointOfInterestListeners.Contains(listener) ||
                ContainsDeferredListener(_deferredPoiRegisterListeners, _deferredPoiRegisterCount, listener))
            {
                return;
            }

            if (_deferredPoiRegisterCount >= PointOfInterestListenerCapacity)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredPoiRegisterListeners[_deferredPoiRegisterCount++] = listener;
        }

        private static void QueueDeferredPointOfInterestUnregister(INarrativePointOfInterestListener listener)
        {
            if (RemoveDeferredListener(_deferredPoiRegisterListeners, ref _deferredPoiRegisterCount, listener))
                return;

            if (!_pointOfInterestListeners.Contains(listener) ||
                ContainsDeferredListener(_deferredPoiUnregisterListeners, _deferredPoiUnregisterCount, listener))
            {
                return;
            }

            if (_deferredPoiUnregisterCount >= PointOfInterestListenerCapacity)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredPoiUnregisterListeners[_deferredPoiUnregisterCount++] = listener;
        }

        private static bool IsDeferredPointOfInterestUnregisterPending(INarrativePointOfInterestListener listener)
        {
            return ContainsDeferredListener(_deferredPoiUnregisterListeners, _deferredPoiUnregisterCount, listener);
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                INarrativeEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                INarrativeEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void ApplyDeferredPointOfInterestListenerMutations()
        {
            for (int i = 0; i < _deferredPoiUnregisterCount; i++)
            {
                INarrativePointOfInterestListener listener = _deferredPoiUnregisterListeners[i];
                _deferredPoiUnregisterListeners[i] = null;
                if (listener != null && _pointOfInterestListeners.Contains(listener))
                    _pointOfInterestListeners.Unregister(listener);
            }

            _deferredPoiUnregisterCount = 0;

            for (int i = 0; i < _deferredPoiRegisterCount; i++)
            {
                INarrativePointOfInterestListener listener = _deferredPoiRegisterListeners[i];
                _deferredPoiRegisterListeners[i] = null;
                if (listener != null)
                    RegisterPointOfInterestImmediate(listener);
            }

            _deferredPoiRegisterCount = 0;
        }

        private static bool ContainsDeferredListener<TListener>(
            TListener[] listeners,
            int listenerCount,
            TListener listener)
            where TListener : class
        {
            for (int i = 0; i < listenerCount; i++)
            {
                if (ReferenceEquals(listeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool RemoveDeferredListener<TListener>(
            TListener[] listeners,
            ref int listenerCount,
            TListener listener)
            where TListener : class
        {
            for (int i = 0; i < listenerCount; i++)
            {
                if (!ReferenceEquals(listeners[i], listener))
                    continue;

                listenerCount--;
                listeners[i] = listeners[listenerCount];
                listeners[listenerCount] = null;
                return true;
            }

            return false;
        }

        private static void RegisterPointOfInterestImmediate(INarrativePointOfInterestListener listener)
        {
            if (!_pointOfInterestListeners.Contains(listener))
                _pointOfInterestListeners.Register(listener);
        }

        private static void RegisterImmediate(INarrativeEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportQueueOverflow(ushort eventType)
        {
            _droppedEventCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NarrativeQueueOverflowWarningHash,
                NarrativeQueueContextHash ^ ((uint)eventType << 24),
                UnityEngine.Mathf.Max(1, _droppedEventCount));
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NarrativeListenerOverflowWarningHash,
                NarrativeListenerContextHash,
                UnityEngine.Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                NarrativeListenerExceptionWarningHash,
                NarrativeListenerExceptionContextHash,
                UnityEngine.Mathf.Max(1, _listenerExceptionCount));
        }
    }
}
