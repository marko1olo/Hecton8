using System;
using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;

namespace Hecton8.Core
{
    internal static class ScalabilityTierRuntime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static HectonQualityTier ToQualityTier(byte tier)
        {
            return ScalabilityTierProfiles.Normalize(tier) == ScalabilityTierProfiles.LowMx350
                ? HectonQualityTier.Mx350
                : HectonQualityTier.High;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte FromQualityTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra
                ? ScalabilityTierProfiles.HighRtx
                : ScalabilityTierProfiles.LowMx350;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MathPrecisionLevel ToMathPrecisionLevel(byte tier)
        {
            return ScalabilityTierProfiles.Normalize(tier) == ScalabilityTierProfiles.LowMx350
                ? MathPrecisionLevel.Low
                : MathPrecisionLevel.High;
        }
    }

    /// <summary>
    /// Listener contract for platform scalability profile changes.
    /// </summary>
    public interface IScalabilityChangedEventListener
    {
        /// <summary>Receives one scalability profile change on the dispatcher event lane.</summary>
        /// <param name="payload">Profile transition payload.</param>
        void OnScalabilityChanged(in ScalabilityChangedEvent payload);
    }

    /// <summary>
    /// Typed scalability event lane drained by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class ScalabilityEvents
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 4;

        // COLD ALLOC: RegistryBucket<IScalabilityChangedEventListener>[32] - platform scalability listeners drained by SystemDispatcher - owner: ScalabilityEvents
        private static readonly RegistryBucket<IScalabilityChangedEventListener> _listeners =
            new RegistryBucket<IScalabilityChangedEventListener>(ListenerCapacity);
        // COLD ALLOC: IScalabilityChangedEventListener[32] - listener additions deferred during scalability dispatch - owner: ScalabilityEvents
        private static readonly IScalabilityChangedEventListener[] _deferredRegisterListeners =
            new IScalabilityChangedEventListener[ListenerCapacity];
        // COLD ALLOC: IScalabilityChangedEventListener[32] - listener removals deferred during scalability dispatch - owner: ScalabilityEvents
        private static readonly IScalabilityChangedEventListener[] _deferredUnregisterListeners =
            new IScalabilityChangedEventListener[ListenerCapacity];

        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _dispatchedSnapshotFrame = -1;
        private static int _dispatchedSnapshotIndex;
        private static bool _isDispatching;
        private static bool _typedSignalLaneConfigured;

        /// <summary>Number of queued scalability events waiting for dispatcher flush.</summary>
        public static int PendingCount
        {
            get
            {
                int snapshotCount = global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.SnapshotCount;
                return _dispatchedSnapshotFrame == Time.frameCount
                    ? Math.Max(0, snapshotCount - _dispatchedSnapshotIndex)
                    : snapshotCount;
            }
        }

        internal static int DroppedEventCount => global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.DroppedLastFlush;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _dispatchedSnapshotFrame = -1;
            _dispatchedSnapshotIndex = 0;
            _isDispatching = false;
            _typedSignalLaneConfigured = false;
        }

        /// <summary>Registers a listener for dispatcher-flushed scalability events.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IScalabilityChangedEventListener listener)
        {
            if (listener == null)
                return;

            EnsureTypedSignalLaneConfigured();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            _listeners.TryRegister(listener);
        }

        /// <summary>Unregisters a scalability listener.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IScalabilityChangedEventListener listener)
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

        /// <summary>Queues one scalability change event.</summary>
        /// <param name="payload">Profile transition payload.</param>
        public static void Raise(in ScalabilityChangedEvent payload)
        {
            EnsureTypedSignalLaneConfigured();
            global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.Push(in payload);
        }

        /// <summary>Flushes queued scalability events to listeners on the main dispatcher lane.</summary>
        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
                return;

            EnsureTypedSignalLaneConfigured();
            ReadOnlySpan<ScalabilityChangedEvent> snapshot =
                global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            int count = snapshot.Length;
            if (count <= 0)
                return;

            int frame = Time.frameCount;
            if (_dispatchedSnapshotFrame != frame)
            {
                _dispatchedSnapshotFrame = frame;
                _dispatchedSnapshotIndex = 0;
            }

            while (_dispatchedSnapshotIndex < count)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                ScalabilityChangedEvent payload = snapshot[_dispatchedSnapshotIndex++];

                IScalabilityChangedEventListener[] rawArray = _listeners.RawArray;
                int listenerCount = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        IScalabilityChangedEventListener listener = rawArray[i];
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        listener.OnScalabilityChanged(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }
        }

        private static void EnsureTypedSignalLaneConfigured()
        {
            if (_typedSignalLaneConfigured)
                return;

            global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.Configure(
                PendingEventCapacity,
                maxFrameSignals: PendingEventCapacity,
                lowTierFrameSignals: PendingEventCapacity,
                laneHash: HectonSignalLaneContract.ScalabilityChangedEventStableHash);
            global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.EnsureInitialized();
            _typedSignalLaneConfigured = true;
        }

        private static void QueueDeferredRegister(IScalabilityChangedEventListener listener)
        {
            if (_deferredRegisterCount >= ListenerCapacity)
                return;

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IScalabilityChangedEventListener listener)
        {
            if (_deferredUnregisterCount >= ListenerCapacity)
                return;

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool IsDeferredUnregisterPending(IScalabilityChangedEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IScalabilityChangedEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IScalabilityChangedEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryRegister(listener);
            }

            _deferredRegisterCount = 0;
        }
    }
}
