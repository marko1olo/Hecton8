using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Mathematics;
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
        private static readonly PowerGridTelemetrySnapshot[] _pendingEvents = new PowerGridTelemetrySnapshot[PendingEventCapacity];
        private static readonly PowerGridTelemetrySnapshot[] _nextFrameEvents = new PowerGridTelemetrySnapshot[PendingEventCapacity];
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
            if (_listenerCount <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = math.min(_pendingEventCount, PendingEventCapacity);
            while (scanBudget-- > 0 && _pendingEventCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                PowerGridTelemetrySnapshot snapshot = _pendingEvents[0];
                ShiftLeft(_pendingEvents, ref _pendingEventCount);

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

            if (_pendingEventCount <= 0)
                PromoteNextFrameEventsIfFrontEmpty();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            ClearEvents(_pendingEvents, ref _pendingEventCount);
            ClearEvents(_nextFrameEvents, ref _nextFrameEventCount);
            _listenerCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Publishes one aggregate runtime snapshot.
        /// </summary>
        public static bool TryRaise(in PowerGridTelemetrySnapshot snapshot)
        {
            if (_listenerCount <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            if (_isDispatching)
            {
                return TryAppend(_nextFrameEvents, ref _nextFrameEventCount, in snapshot);
            }

            return TryAppend(_pendingEvents, ref _pendingEventCount, in snapshot);
        }

        [System.Obsolete("Use TryRaise so bounded queue refusal is visible at the producer.", true)]
        public static void Raise(in PowerGridTelemetrySnapshot snapshot) => TryRaise(in snapshot);

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(_pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(_pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            DrainQueueWithoutDispatch(_nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            PowerGridTelemetrySnapshot[] queue,
            ref int pendingCount)
        {
            int scanBudget = math.min(pendingCount, PendingEventCapacity);
            while (scanBudget-- > 0 && pendingCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                ShiftLeft(queue, ref pendingCount);
            }

            if (pendingCount <= 0)
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (_pendingEventCount > 0 || _nextFrameEventCount <= 0)
            {
                return;
            }

            int count = math.min(_nextFrameEventCount, PendingEventCapacity);
            for (int i = 0; i < count; i++)
                _pendingEvents[i] = _nextFrameEvents[i];

            _pendingEventCount = count;
            ClearEvents(_nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool TryAppend(PowerGridTelemetrySnapshot[] queue, ref int count, in PowerGridTelemetrySnapshot snapshot)
        {
            if (count < 0 || count >= PendingEventCapacity)
                return false;

            queue[count++] = snapshot;
            return true;
        }

        private static void ShiftLeft(PowerGridTelemetrySnapshot[] queue, ref int count)
        {
            if (count <= 0)
                return;

            int last = count - 1;
            for (int i = 0; i < last; i++)
                queue[i] = queue[i + 1];

            queue[last] = default;
            count = last;
        }

        private static void ClearEvents(PowerGridTelemetrySnapshot[] queue, ref int count)
        {
            int safeCount = math.min(math.max(0, count), PendingEventCapacity);
            for (int i = 0; i < safeCount; i++)
                queue[i] = default;

            count = 0;
        }
    }
}
