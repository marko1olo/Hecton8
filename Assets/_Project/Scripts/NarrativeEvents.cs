using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Interaction;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Core
{
    public enum NarrativeEventType : byte
    {
        DiscoveryMade = 0,
        DepthTierReached = 1,
        AudioLogFound = 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct NarrativeEventPayload
    {
        [FieldOffset(0)] public uint DiscoveryHash;
        [FieldOffset(4)] public ushort EventType;
        [FieldOffset(6)] public short DepthTier;
        [FieldOffset(8)] private ulong _pad0;
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
        private const int DiscoveryIdCapacity = 64;
        private const uint NarrativeListenerOverflowWarningHash = 0x4E41564Cu; // NAVL
        private const uint NarrativeListenerContextHash = 0x4E415652u; // NAVR
        private const uint NarrativeListenerExceptionWarningHash = 0x4E415645u; // NAVE
        private const uint NarrativeListenerExceptionContextHash = 0x4E415658u; // NAVX
        private const uint NarrativeQueueOverflowWarningHash = 0x4E415651u; // NAVQ
        private const uint NarrativeQueueContextHash = 0x4E415650u; // NAVP
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct NarrativeListenerSlot
        {
            public INarrativeEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct NarrativePointOfInterestListenerSlot
        {
            public INarrativePointOfInterestListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct DiscoveryIdSlot
        {
            public uint DiscoveryHash;
            public string DiscoveryId;
            public byte IsValid;

            public void Clear()
            {
                DiscoveryHash = 0u;
                DiscoveryId = null;
                IsValid = 0;
            }
        }

        private struct NarrativeListenerRegistry
        {
            private readonly NarrativeListenerSlot[] _slots;
            private int _count;

            public NarrativeListenerRegistry(int capacity)
            {
                _slots = new NarrativeListenerSlot[capacity]; // COLD ALLOC: NarrativeListenerSlot[16] - fixed narrative listeners drained on dispatcher LateUpdate - owner: NarrativeEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(INarrativeEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(INarrativeEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(INarrativeEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public INarrativeEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private struct NarrativePointOfInterestListenerRegistry
        {
            private readonly NarrativePointOfInterestListenerSlot[] _slots;
            private int _count;

            public NarrativePointOfInterestListenerRegistry(int capacity)
            {
                _slots = new NarrativePointOfInterestListenerSlot[capacity]; // COLD ALLOC: NarrativePointOfInterestListenerSlot[8] - fixed narrative POI listeners - owner: NarrativeEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(INarrativePointOfInterestListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(INarrativePointOfInterestListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(INarrativePointOfInterestListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public INarrativePointOfInterestListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static NarrativeListenerRegistry _listeners = new NarrativeListenerRegistry(ListenerCapacity);
        // COLD ALLOC: NarrativeListenerSlot[16] - listener additions deferred while dispatching narrative events - owner: NarrativeEvents
        private static readonly NarrativeListenerSlot[] _deferredRegisterListeners = new NarrativeListenerSlot[ListenerCapacity];
        // COLD ALLOC: NarrativeListenerSlot[16] - listener removals deferred while dispatching narrative events - owner: NarrativeEvents
        private static readonly NarrativeListenerSlot[] _deferredUnregisterListeners = new NarrativeListenerSlot[ListenerCapacity];
        private static NarrativePointOfInterestListenerRegistry _pointOfInterestListeners = new NarrativePointOfInterestListenerRegistry(PointOfInterestListenerCapacity);
        // COLD ALLOC: NarrativePointOfInterestListenerSlot[8] - POI listener additions deferred while dispatching direct callbacks - owner: NarrativeEvents
        private static readonly NarrativePointOfInterestListenerSlot[] _deferredPoiRegisterListeners = new NarrativePointOfInterestListenerSlot[PointOfInterestListenerCapacity];
        // COLD ALLOC: NarrativePointOfInterestListenerSlot[8] - POI listener removals deferred while dispatching direct callbacks - owner: NarrativeEvents
        private static readonly NarrativePointOfInterestListenerSlot[] _deferredPoiUnregisterListeners = new NarrativePointOfInterestListenerSlot[PointOfInterestListenerCapacity];
        // COLD ALLOC: DiscoveryIdSlot[64] - fixed hashed narrative discovery id lookup for cold listener resolution - owner: NarrativeEvents
        private static readonly DiscoveryIdSlot[] _discoveryIdsByHash = new DiscoveryIdSlot[DiscoveryIdCapacity];
        private static NativeQueue<NarrativeEventPayload> _pendingEvents;
        private static NativeQueue<NarrativeEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _discoveryIdCount;
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
            ReleaseNativeQueues();

            _listeners.Clear();
            _pointOfInterestListeners.Clear();
            ClearDiscoveryIds();
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
                _pointOfInterestListeners.TryUnregister(listener);
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
                        INarrativeEventListener listener = _listeners.GetAt(i);
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
            return TryResolveDiscoveryIdSlot(discoveryHash, out discoveryId);
        }

        public static bool TryNotifyNarrativePOIRegistered(NarrativeDiscovery poi)
        {
            if (poi == null || _pointOfInterestListeners.Count <= 0)
                return false;

            int count = _pointOfInterestListeners.Count;
            _isDispatchingPointOfInterest = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    INarrativePointOfInterestListener listener = _pointOfInterestListeners.GetAt(i);
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

            return true;
        }

        [Obsolete("Narrative POI direct callbacks must use TryNotifyNarrativePOIRegistered so dispatch success is explicit.", true)]
        public static void RaiseNarrativePOIRegistered(NarrativeDiscovery poi)
        {
            TryNotifyNarrativePOIRegistered(poi);
        }

        public static bool TryNotifyNarrativePOIDisposed(NarrativeDiscovery poi)
        {
            if (poi == null || _pointOfInterestListeners.Count <= 0)
                return false;

            int count = _pointOfInterestListeners.Count;
            _isDispatchingPointOfInterest = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    INarrativePointOfInterestListener listener = _pointOfInterestListeners.GetAt(i);
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

            return true;
        }

        [Obsolete("Narrative POI direct callbacks must use TryNotifyNarrativePOIDisposed so dispatch success is explicit.", true)]
        public static void RaiseNarrativePOIDisposed(NarrativeDiscovery poi)
        {
            TryNotifyNarrativePOIDisposed(poi);
        }

        [Obsolete("Use TryRaiseDiscoveryMade(uint discoveryHash). String ingress is not allowed on first-party event lanes.", true)]
        public static void RaiseDiscoveryMade(string discoveryId)
        {
            TryRaiseDiscoveryMadeFromString(discoveryId);
        }

        private static bool TryRaiseDiscoveryMadeFromString(string discoveryId)
        {
            uint discoveryHash = ComputeDiscoveryHash(discoveryId);
            if (discoveryHash == 0u)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow((ushort)NarrativeEventType.DiscoveryMade);
                return false;
            }

            if (!TryRegisterDiscoveryId(discoveryHash, discoveryId))
            {
                ReportQueueOverflow((ushort)NarrativeEventType.DiscoveryMade);
                return false;
            }

            return TryRaiseDiscoveryMade(discoveryHash);
        }

        [Obsolete("Use TryRaiseDiscoveryMade(uint discoveryHash) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool RaiseDiscoveryMade(uint discoveryHash)
        {
            return TryRaiseDiscoveryMade(discoveryHash);
        }

        public static bool TryRaiseDiscoveryMade(uint discoveryHash)
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

        [Obsolete("Use TryRaiseAudioLogFound(uint logHash). String ingress is not allowed on first-party event lanes.", true)]
        public static void RaiseAudioLogFound(string logId)
        {
            TryRaiseAudioLogFoundFromString(logId);
        }

        private static bool TryRaiseAudioLogFoundFromString(string logId)
        {
            uint logHash = ComputeDiscoveryHash(logId);
            if (logHash == 0u)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow((ushort)NarrativeEventType.AudioLogFound);
                return false;
            }

            if (!TryRegisterDiscoveryId(logHash, logId))
            {
                ReportQueueOverflow((ushort)NarrativeEventType.AudioLogFound);
                return false;
            }

            return TryRaiseAudioLogFound(logHash);
        }

        [Obsolete("Use TryRaiseAudioLogFound(uint logHash) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool RaiseAudioLogFound(uint logHash)
        {
            return TryRaiseAudioLogFound(logHash);
        }

        public static bool TryRaiseAudioLogFound(uint logHash)
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

        [Obsolete("Use TryRaiseDepthTierReached(int tier) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseDepthTierReached(int tier)
        {
            TryRaiseDepthTierReached(tier);
        }

        public static bool TryRaiseDepthTierReached(int tier)
        {
            return Enqueue(new NarrativeEventPayload
            {
                DiscoveryHash = 0u,
                EventType = (ushort)NarrativeEventType.DepthTierReached,
                DepthTier = (short)tier
            });
        }

        private static bool TryRegisterDiscoveryId(uint discoveryHash, string discoveryId)
        {
            if (discoveryHash == 0u)
                return false;

            if (TryFindDiscoveryId(discoveryHash, out _))
                return true;

            if (_discoveryIdCount >= _discoveryIdsByHash.Length)
                return false;

            _discoveryIdsByHash[_discoveryIdCount++] = new DiscoveryIdSlot
            {
                DiscoveryHash = discoveryHash,
                DiscoveryId = discoveryId,
                IsValid = 1
            };
            return true;
        }

        private static bool TryResolveDiscoveryIdSlot(uint discoveryHash, out string discoveryId)
        {
            if (TryFindDiscoveryId(discoveryHash, out int index))
            {
                discoveryId = _discoveryIdsByHash[index].DiscoveryId ?? string.Empty;
                return true;
            }

            discoveryId = string.Empty;
            return false;
        }

        private static bool TryFindDiscoveryId(uint discoveryHash, out int index)
        {
            for (int i = 0; i < _discoveryIdCount; i++)
            {
                DiscoveryIdSlot slot = _discoveryIdsByHash[i];
                if (slot.IsValid != 0 && slot.DiscoveryHash == discoveryHash)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static void ClearDiscoveryIds()
        {
            for (int i = 0; i < _discoveryIdCount; i++)
                _discoveryIdsByHash[i].Clear();

            _discoveryIdCount = 0;
        }

        private static bool Enqueue(in NarrativeEventPayload payload)
        {
            // Batchmode headless probes: NativeQueue ctor / SetStaticSafetyId can native-crash
            // under mono JIT during world activation (DepthZoneDirector discovery raise).
            // Narrative presentation is not required for hop/input validation.
            if (Application.isBatchMode)
                return false;

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
            if (Application.isBatchMode)
                return;

            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<NarrativeEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<NarrativeEventPayload>[16] - deferred narrative event lane flushed by SystemDispatcher LateUpdate - owner: NarrativeEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<NarrativeEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<NarrativeEventPayload>[16] - next-frame narrative event lane prevents same-frame reentrant dispatch - owner: NarrativeEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                ClearDiscoveryIds();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(NarrativeEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
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
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<NarrativeEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
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
            Hecton8.Core.H8Debug.LogException(exception);
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
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

            _deferredPoiRegisterListeners[_deferredPoiRegisterCount++].Listener = listener;
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

            _deferredPoiUnregisterListeners[_deferredPoiUnregisterCount++].Listener = listener;
        }

        private static bool IsDeferredPointOfInterestUnregisterPending(INarrativePointOfInterestListener listener)
        {
            return ContainsDeferredListener(_deferredPoiUnregisterListeners, _deferredPoiUnregisterCount, listener);
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                INarrativeEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                INarrativeEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void ApplyDeferredPointOfInterestListenerMutations()
        {
            for (int i = 0; i < _deferredPoiUnregisterCount; i++)
            {
                INarrativePointOfInterestListener listener = _deferredPoiUnregisterListeners[i].Listener;
                _deferredPoiUnregisterListeners[i].Clear();
                if (listener != null && _pointOfInterestListeners.Contains(listener))
                    _pointOfInterestListeners.TryUnregister(listener);
            }

            _deferredPoiUnregisterCount = 0;

            for (int i = 0; i < _deferredPoiRegisterCount; i++)
            {
                INarrativePointOfInterestListener listener = _deferredPoiRegisterListeners[i].Listener;
                _deferredPoiRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterPointOfInterestImmediate(listener);
            }

            _deferredPoiRegisterCount = 0;
        }

        private static bool ContainsDeferredListener(
            NarrativeListenerSlot[] listeners,
            int listenerCount,
            INarrativeEventListener listener)
        {
            for (int i = 0; i < listenerCount; i++)
            {
                if (ReferenceEquals(listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool ContainsDeferredListener(
            NarrativePointOfInterestListenerSlot[] listeners,
            int listenerCount,
            INarrativePointOfInterestListener listener)
        {
            for (int i = 0; i < listenerCount; i++)
            {
                if (ReferenceEquals(listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool RemoveDeferredListener(
            NarrativeListenerSlot[] listeners,
            ref int listenerCount,
            INarrativeEventListener listener)
        {
            for (int i = 0; i < listenerCount; i++)
            {
                if (!ReferenceEquals(listeners[i].Listener, listener))
                    continue;

                listenerCount--;
                listeners[i] = listeners[listenerCount];
                listeners[listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static bool RemoveDeferredListener(
            NarrativePointOfInterestListenerSlot[] listeners,
            ref int listenerCount,
            INarrativePointOfInterestListener listener)
        {
            for (int i = 0; i < listenerCount; i++)
            {
                if (!ReferenceEquals(listeners[i].Listener, listener))
                    continue;

                listenerCount--;
                listeners[i] = listeners[listenerCount];
                listeners[listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static void RegisterPointOfInterestImmediate(INarrativePointOfInterestListener listener)
        {
            if (_pointOfInterestListeners.Contains(listener))
                return;

            if (!_pointOfInterestListeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
