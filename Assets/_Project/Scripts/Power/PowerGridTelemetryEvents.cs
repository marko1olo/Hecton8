using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Aggregate runtime power snapshot published by <see cref="PowerGridManager"/>.
    /// </summary>
    public readonly struct PowerGridTelemetrySnapshot
    {
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
            HighestBrownoutTier = highestBrownoutTier;
            HasPowerDeficit = hasPowerDeficit;
            EmergencyReserveActive = emergencyReserveActive;
        }

        /// <summary>Connected runtime grid count.</summary>
        public int GridCount { get; }

        /// <summary>How many grids currently report a deficit.</summary>
        public int DeficitGridCount { get; }

        /// <summary>Total authored generation in watts.</summary>
        public float TotalGeneration { get; }

        /// <summary>Total authored demand in watts.</summary>
        public float TotalConsumption { get; }

        /// <summary>Aggregate generation to demand ratio for the current pass.</summary>
        public float SupplyRatio { get; }

        /// <summary>Aggregate battery charge normalized to 0..1.</summary>
        public float BatteryChargeNormalized { get; }

        /// <summary>
        /// Best-effort runtime power health normalized to 0..1.
        /// Uses battery charge when storage exists; otherwise falls back to supply ratio.
        /// </summary>
        public float AvailablePowerNormalized { get; }

        /// <summary>Worst brownout tier observed across all active grids.</summary>
        public LogisticsBrownoutTier HighestBrownoutTier { get; }

        /// <summary>True while any grid is undersupplied.</summary>
        public bool HasPowerDeficit { get; }

        /// <summary>True while any battery bank is in emergency-reserve mode.</summary>
        public bool EmergencyReserveActive { get; }
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

        // COLD ALLOC: RegistryBucket<IPowerGridTelemetryListener>[8] - power telemetry listeners drained by SystemDispatcher LateUpdate - owner: PowerGridTelemetryEvents
        private static readonly RegistryBucket<IPowerGridTelemetryListener> _listeners = new RegistryBucket<IPowerGridTelemetryListener>(ListenerCapacity);
        private static NativeQueue<PowerGridTelemetrySnapshot> _pendingEvents;
        private static NativeQueue<PowerGridTelemetrySnapshot> _nextFrameEvents;
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
            _listeners.TryRegister(listener);
        }

        /// <summary>
        /// Unregisters a power telemetry listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IPowerGridTelemetryListener listener)
        {
            if (listener == null || !_listeners.Contains(listener))
                return;

            _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued telemetry snapshots through registered listeners.
        /// </summary>
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

                if (!_pendingEvents.TryDequeue(out PowerGridTelemetrySnapshot snapshot))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IPowerGridTelemetryListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                        rawArray[i].OnPowerGridTelemetryUpdated(in snapshot);
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
        private static void ResetStaticState()
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

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Publishes one aggregate runtime snapshot.
        /// </summary>
        public static void Raise(in PowerGridTelemetrySnapshot snapshot)
        {
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
                _pendingEvents = new NativeQueue<PowerGridTelemetrySnapshot>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PowerGridTelemetrySnapshot>[8] - deferred aggregate power telemetry lane flushed by SystemDispatcher LateUpdate - owner: PowerGridTelemetryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PowerGridTelemetryEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PowerGridTelemetrySnapshot>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PowerGridTelemetrySnapshot>[8] - next-frame power telemetry lane prevents same-frame reentrant dispatch - owner: PowerGridTelemetryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PowerGridTelemetryEvents),
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
