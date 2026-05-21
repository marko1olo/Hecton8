using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Aggregate runtime power snapshot published by <see cref="PowerGridManager"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct PowerGridTelemetrySnapshot
    {
        private const uint HasPowerDeficitMask = 1u << 0;
        private const uint EmergencyReserveActiveMask = 1u << 1;
        private const int BrownoutTierShift = 8;
        private const uint BrownoutTierMask = 0xFFu << BrownoutTierShift;

        public PowerGridTelemetrySnapshot(
            int gridCount,
            int deficitGridCount,
            float totalGeneration,
            float totalConsumption,
            float supplyRatio,
            float batteryChargeNormalized,
            float availablePowerNormalized,
            LogisticsBrownoutTier highestBrownoutTier,
            bool hasPowerDeficit,
            bool emergencyReserveActive)
        {
            GridCount = gridCount;
            DeficitGridCount = deficitGridCount;
            TotalGeneration = totalGeneration;
            TotalConsumption = totalConsumption;
            SupplyRatio = supplyRatio;
            BatteryChargeNormalized = batteryChargeNormalized;
            AvailablePowerNormalized = availablePowerNormalized;
            StatusFlags = PackStatusFlags(highestBrownoutTier, hasPowerDeficit, emergencyReserveActive);
        }

        /// <summary>Connected runtime grid count.</summary>
        [FieldOffset(0)] public readonly int GridCount;

        /// <summary>How many grids currently report a deficit.</summary>
        [FieldOffset(4)] public readonly int DeficitGridCount;

        /// <summary>Total authored generation in watts.</summary>
        [FieldOffset(8)] public readonly float TotalGeneration;

        /// <summary>Total authored demand in watts.</summary>
        [FieldOffset(12)] public readonly float TotalConsumption;

        /// <summary>Aggregate generation to demand ratio for the current pass.</summary>
        [FieldOffset(16)] public readonly float SupplyRatio;

        /// <summary>Aggregate battery charge normalized to 0..1.</summary>
        [FieldOffset(20)] public readonly float BatteryChargeNormalized;

        /// <summary>
        /// Best-effort runtime power health normalized to 0..1.
        /// Uses battery charge when storage exists; otherwise falls back to supply ratio.
        /// </summary>
        [FieldOffset(24)] public readonly float AvailablePowerNormalized;

        /// <summary>Bit-packed deficit, reserve, and brownout-tier status.</summary>
        [FieldOffset(28)] public readonly uint StatusFlags;

        public static bool HasPowerDeficit(in PowerGridTelemetrySnapshot snapshot)
        {
            return (snapshot.StatusFlags & HasPowerDeficitMask) != 0u;
        }

        public static bool IsEmergencyReserveActive(in PowerGridTelemetrySnapshot snapshot)
        {
            return (snapshot.StatusFlags & EmergencyReserveActiveMask) != 0u;
        }

        public static LogisticsBrownoutTier GetHighestBrownoutTier(in PowerGridTelemetrySnapshot snapshot)
        {
            uint tier = (snapshot.StatusFlags & BrownoutTierMask) >> BrownoutTierShift;
            return (LogisticsBrownoutTier)tier;
        }

        private static uint PackStatusFlags(
            LogisticsBrownoutTier highestBrownoutTier,
            bool hasPowerDeficit,
            bool emergencyReserveActive)
        {
            uint flags = (unchecked((uint)(int)highestBrownoutTier) & 0xFFu) << BrownoutTierShift;
            if (hasPowerDeficit)
                flags |= HasPowerDeficitMask;
            if (emergencyReserveActive)
                flags |= EmergencyReserveActiveMask;
            return flags;
        }
    }

    /// <summary>
    /// Listener contract for aggregate power telemetry snapshots.
    /// </summary>
    public interface IPowerGridTelemetryListener
    {
        /// <summary>
        /// Receives one aggregate power telemetry snapshot during dispatcher LateUpdate.
        /// </summary>
        /// <param name="snapshot">Latest aggregate power telemetry snapshot.</param>
        void OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot);
    }

    /// <summary>
    /// Queue-backed aggregate power telemetry bus for submarine and HUD observers.
    /// </summary>
    public static class PowerGridTelemetryEvents
    {
        private const int PendingEventCapacity = 8;
        private const int ListenerCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IPowerGridTelemetryListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - power telemetry listeners drained by SystemDispatcher LateUpdate - owner: PowerGridTelemetryEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<PowerGridTelemetrySnapshot> _pendingEvents;
        private static NativeQueue<PowerGridTelemetrySnapshot> _nextFrameEvents;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Pending aggregate telemetry snapshots.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Registers a power telemetry listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IPowerGridTelemetryListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        /// <summary>
        /// Unregisters a power telemetry listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IPowerGridTelemetryListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return;
            }
        }

        /// <summary>
        /// Flushes queued telemetry snapshots through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listenerCount <= 0)
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

                if (!_pendingEvents.TryDequeue(out PowerGridTelemetrySnapshot snapshot))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IPowerGridTelemetryListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnPowerGridTelemetryUpdated(in snapshot);
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PowerGridTelemetryEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PowerGridTelemetryEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Publishes one aggregate runtime snapshot.
        /// </summary>
        public static void Raise(in PowerGridTelemetrySnapshot snapshot)
        {
            if (_listenerCount <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(snapshot);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(snapshot);
            _pendingEventCount++;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PowerGridTelemetrySnapshot>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PowerGridTelemetrySnapshot>[8] — deferred aggregate power telemetry lane flushed by SystemDispatcher LateUpdate — owner: PowerGridTelemetryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PowerGridTelemetryEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PowerGridTelemetrySnapshot>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PowerGridTelemetrySnapshot>[8] — next-frame power telemetry lane prevents same-frame reentrant dispatch — owner: PowerGridTelemetryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PowerGridTelemetryEvents),
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
            ref NativeQueue<PowerGridTelemetrySnapshot> queue,
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

            NativeQueue<PowerGridTelemetrySnapshot> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
