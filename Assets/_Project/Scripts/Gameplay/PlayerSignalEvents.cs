using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// HUD-directed trauma packet raised by runtime damage owners without scene polling.
    /// </summary>
    public readonly struct TraumaHudSignal
    {
        public TraumaHudSignal(
            float glitchIntensity,
            float recoilScalar,
            float transportPower01,
            float hullIntegrity01,
            bool biosRecoveryMode)
        {
            GlitchIntensity = glitchIntensity;
            RecoilScalar = recoilScalar;
            TransportPower01 = transportPower01;
            HullIntegrity01 = hullIntegrity01;
            BiosRecoveryMode = biosRecoveryMode;
        }

        public float GlitchIntensity { get; }
        public float RecoilScalar { get; }
        public float TransportPower01 { get; }
        public float HullIntegrity01 { get; }
        public bool BiosRecoveryMode { get; }
    }

    /// <summary>
    /// Audio-directed internal stress packet for heartbeat / breathing consumers.
    /// </summary>
    public readonly struct InteractionSignal
    {
        public InteractionSignal(
            float stress01,
            float volume01,
            float pitchScale,
            float frequency01)
        {
            Stress01 = stress01;
            Volume01 = volume01;
            PitchScale = pitchScale;
            Frequency01 = frequency01;
        }

        public float Stress01 { get; }
        public float Volume01 { get; }
        public float PitchScale { get; }
        public float Frequency01 { get; }
    }

    /// <summary>
    /// Raised when the equipped tool is exhausted and removed from the inventory.
    /// </summary>
    public readonly struct ToolDepletedSignal
    {
        public ToolDepletedSignal(int toolHashId)
        {
            ToolHashId = toolHashId;
        }

        public int ToolHashId { get; }
    }

    /// <summary>
    /// Listener contract for deferred player signal dispatch.
    /// </summary>
    public interface IPlayerSignalEventListener
    {
        /// <summary>Called when trauma state should update the HUD.</summary>
        /// <param name="signal">Trauma HUD payload.</param>
        void OnTraumaHudSignal(in TraumaHudSignal signal);

        /// <summary>Called when player interaction stress should update VFX/audio coupling.</summary>
        /// <param name="signal">Interaction stress payload.</param>
        void OnInteractionSignal(in InteractionSignal signal);

        /// <summary>Called when an equipped tool was depleted.</summary>
        /// <param name="signal">Tool depletion payload.</param>
        void OnToolDepletedSignal(in ToolDepletedSignal signal);
    }

    /// <summary>
    /// Static queue-backed signal bus for trauma, HUD, and internal audio coupling.
    /// </summary>
    public static class PlayerSignalEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingTraumaHudCapacity = 16;
        private const int PendingInteractionSignalCapacity = 16;
        private const int PendingToolDepletedCapacity = 16;

        // COLD ALLOC: RegistryBucket<IPlayerSignalEventListener>[16] - deferred player-signal listeners - owner: PlayerSignalEvents
        private static readonly RegistryBucket<IPlayerSignalEventListener> _listeners = new RegistryBucket<IPlayerSignalEventListener>(ListenerCapacity);
        private static NativeQueue<TraumaHudSignal> _pendingTraumaHudSignals;
        private static NativeQueue<TraumaHudSignal> _nextFrameTraumaHudSignals;
        private static NativeQueue<InteractionSignal> _pendingInteractionSignals;
        private static NativeQueue<InteractionSignal> _nextFrameInteractionSignals;
        private static NativeQueue<ToolDepletedSignal> _pendingToolDepletedSignals;
        private static NativeQueue<ToolDepletedSignal> _nextFrameToolDepletedSignals;
        private static int _pendingTraumaHudSignalCount;
        private static int _nextFrameTraumaHudSignalCount;
        private static int _pendingInteractionSignalCount;
        private static int _nextFrameInteractionSignalCount;
        private static int _pendingToolDepletedSignalCount;
        private static int _nextFrameToolDepletedSignalCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of player signal payloads waiting for the LateUpdate flush lane.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                return _pendingTraumaHudSignalCount
                    + _nextFrameTraumaHudSignalCount
                    + _pendingInteractionSignalCount
                    + _nextFrameInteractionSignalCount
                    + _pendingToolDepletedSignalCount
                    + _nextFrameToolDepletedSignalCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingTraumaHudSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents), nameof(_pendingTraumaHudSignals));
                _pendingTraumaHudSignals.Dispose();
                _pendingTraumaHudSignals = default;
            }

            if (_nextFrameTraumaHudSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents), nameof(_nextFrameTraumaHudSignals));
                _nextFrameTraumaHudSignals.Dispose();
                _nextFrameTraumaHudSignals = default;
            }

            if (_pendingInteractionSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents), nameof(_pendingInteractionSignals));
                _pendingInteractionSignals.Dispose();
                _pendingInteractionSignals = default;
            }

            if (_nextFrameInteractionSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents), nameof(_nextFrameInteractionSignals));
                _nextFrameInteractionSignals.Dispose();
                _nextFrameInteractionSignals = default;
            }

            if (_pendingToolDepletedSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents), nameof(_pendingToolDepletedSignals));
                _pendingToolDepletedSignals.Dispose();
                _pendingToolDepletedSignals = default;
            }

            if (_nextFrameToolDepletedSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerSignalEvents), nameof(_nextFrameToolDepletedSignals));
                _nextFrameToolDepletedSignals.Dispose();
                _nextFrameToolDepletedSignals = default;
            }

            _pendingTraumaHudSignalCount = 0;
            _nextFrameTraumaHudSignalCount = 0;
            _pendingInteractionSignalCount = 0;
            _nextFrameInteractionSignalCount = 0;
            _pendingToolDepletedSignalCount = 0;
            _nextFrameToolDepletedSignalCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        /// <summary>
        /// Registers one listener for deferred player signal delivery.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void Register(IPlayerSignalEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters one listener from deferred player signal delivery.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void Unregister(IPlayerSignalEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued player signals on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listeners.Count <= 0)
                {
                    completed = DrainWithoutDispatch();
                }
                else
                {
                    completed = FlushTraumaSignals();
                    if (completed)
                        completed = FlushInteractionSignals();
                    if (completed)
                        completed = FlushToolDepletedSignals();
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        /// <summary>
        /// Queues one trauma HUD signal.
        /// </summary>
        /// <param name="signal">Signal payload.</param>
        public static void RaiseTraumaHudSignal(in TraumaHudSignal signal)
        {
            EnsureInitialized();
            if (_pendingTraumaHudSignalCount + _nextFrameTraumaHudSignalCount >= PendingTraumaHudCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameTraumaHudSignals.Enqueue(signal);
                _nextFrameTraumaHudSignalCount++;
            }
            else
            {
                _pendingTraumaHudSignals.Enqueue(signal);
                _pendingTraumaHudSignalCount++;
            }
        }

        /// <summary>
        /// Queues one interaction stress signal.
        /// </summary>
        /// <param name="signal">Signal payload.</param>
        public static void RaiseInteractionSignal(in InteractionSignal signal)
        {
            EnsureInitialized();
            if (_pendingInteractionSignalCount + _nextFrameInteractionSignalCount >= PendingInteractionSignalCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameInteractionSignals.Enqueue(signal);
                _nextFrameInteractionSignalCount++;
            }
            else
            {
                _pendingInteractionSignals.Enqueue(signal);
                _pendingInteractionSignalCount++;
            }
        }

        /// <summary>
        /// Queues one tool depletion signal.
        /// </summary>
        /// <param name="signal">Signal payload.</param>
        public static void RaiseToolDepletedSignal(in ToolDepletedSignal signal)
        {
            EnsureInitialized();
            if (_pendingToolDepletedSignalCount + _nextFrameToolDepletedSignalCount >= PendingToolDepletedCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameToolDepletedSignals.Enqueue(signal);
                _nextFrameToolDepletedSignalCount++;
            }
            else
            {
                _pendingToolDepletedSignals.Enqueue(signal);
                _pendingToolDepletedSignalCount++;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingTraumaHudSignals.IsCreated)
            {
                _pendingTraumaHudSignals = new NativeQueue<TraumaHudSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<TraumaHudSignal>[16] - deferred trauma HUD lane - owner: PlayerSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingTraumaHudSignals,
                    PendingTraumaHudCapacity,
                    nameof(PlayerSignalEvents),
                    nameof(_pendingTraumaHudSignals),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameTraumaHudSignals.IsCreated)
            {
                _nextFrameTraumaHudSignals = new NativeQueue<TraumaHudSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<TraumaHudSignal>[16] - next-frame trauma HUD lane - owner: PlayerSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameTraumaHudSignals,
                    PendingTraumaHudCapacity,
                    nameof(PlayerSignalEvents),
                    nameof(_nextFrameTraumaHudSignals),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingInteractionSignals.IsCreated)
            {
                _pendingInteractionSignals = new NativeQueue<InteractionSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionSignal>[16] - deferred interaction stress lane - owner: PlayerSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingInteractionSignals,
                    PendingInteractionSignalCapacity,
                    nameof(PlayerSignalEvents),
                    nameof(_pendingInteractionSignals),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameInteractionSignals.IsCreated)
            {
                _nextFrameInteractionSignals = new NativeQueue<InteractionSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionSignal>[16] - next-frame interaction stress lane - owner: PlayerSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameInteractionSignals,
                    PendingInteractionSignalCapacity,
                    nameof(PlayerSignalEvents),
                    nameof(_nextFrameInteractionSignals),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingToolDepletedSignals.IsCreated)
            {
                _pendingToolDepletedSignals = new NativeQueue<ToolDepletedSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ToolDepletedSignal>[16] - deferred tool depletion lane - owner: PlayerSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingToolDepletedSignals,
                    PendingToolDepletedCapacity,
                    nameof(PlayerSignalEvents),
                    nameof(_pendingToolDepletedSignals),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameToolDepletedSignals.IsCreated)
            {
                _nextFrameToolDepletedSignals = new NativeQueue<ToolDepletedSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ToolDepletedSignal>[16] - next-frame tool depletion lane - owner: PlayerSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameToolDepletedSignals,
                    PendingToolDepletedCapacity,
                    nameof(PlayerSignalEvents),
                    nameof(_nextFrameToolDepletedSignals),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool FlushTraumaSignals()
        {
            if (!_pendingTraumaHudSignals.IsCreated)
                return true;

            int scanBudget = _pendingTraumaHudSignalCount > 0 ? _pendingTraumaHudSignalCount : PendingTraumaHudCapacity;
            while (scanBudget > 0 && !_pendingTraumaHudSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingTraumaHudSignals.TryDequeue(out TraumaHudSignal signal))
                    return true;

                _pendingTraumaHudSignalCount--;
                scanBudget--;
                IPlayerSignalEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IPlayerSignalEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnTraumaHudSignal(in signal);
                }
            }

            if (_pendingTraumaHudSignals.IsEmpty())
                _pendingTraumaHudSignalCount = 0;

            return true;
        }

        private static bool FlushInteractionSignals()
        {
            if (!_pendingInteractionSignals.IsCreated)
                return true;

            int scanBudget = _pendingInteractionSignalCount > 0 ? _pendingInteractionSignalCount : PendingInteractionSignalCapacity;
            while (scanBudget > 0 && !_pendingInteractionSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingInteractionSignals.TryDequeue(out InteractionSignal signal))
                    return true;

                _pendingInteractionSignalCount--;
                scanBudget--;
                IPlayerSignalEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IPlayerSignalEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnInteractionSignal(in signal);
                }
            }

            if (_pendingInteractionSignals.IsEmpty())
                _pendingInteractionSignalCount = 0;

            return true;
        }

        private static bool FlushToolDepletedSignals()
        {
            if (!_pendingToolDepletedSignals.IsCreated)
                return true;

            int scanBudget = _pendingToolDepletedSignalCount > 0 ? _pendingToolDepletedSignalCount : PendingToolDepletedCapacity;
            while (scanBudget > 0 && !_pendingToolDepletedSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingToolDepletedSignals.TryDequeue(out ToolDepletedSignal signal))
                    return true;

                _pendingToolDepletedSignalCount--;
                scanBudget--;
                IPlayerSignalEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IPlayerSignalEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnToolDepletedSignal(in signal);
                }
            }

            if (_pendingToolDepletedSignals.IsEmpty())
                _pendingToolDepletedSignalCount = 0;

            return true;
        }

        private static bool DrainWithoutDispatch()
        {
            if (_pendingTraumaHudSignals.IsCreated)
            {
                int scanBudget = _pendingTraumaHudSignalCount > 0 ? _pendingTraumaHudSignalCount : PendingTraumaHudCapacity;
                while (scanBudget > 0 && !_pendingTraumaHudSignals.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingTraumaHudSignals.TryDequeue(out _))
                        return true;

                    _pendingTraumaHudSignalCount--;
                    scanBudget--;
                }

                if (_pendingTraumaHudSignals.IsEmpty())
                    _pendingTraumaHudSignalCount = 0;
            }

            if (_pendingInteractionSignals.IsCreated)
            {
                int scanBudget = _pendingInteractionSignalCount > 0 ? _pendingInteractionSignalCount : PendingInteractionSignalCapacity;
                while (scanBudget > 0 && !_pendingInteractionSignals.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingInteractionSignals.TryDequeue(out _))
                        return true;

                    _pendingInteractionSignalCount--;
                    scanBudget--;
                }

                if (_pendingInteractionSignals.IsEmpty())
                    _pendingInteractionSignalCount = 0;
            }

            if (_pendingToolDepletedSignals.IsCreated)
            {
                int scanBudget = _pendingToolDepletedSignalCount > 0 ? _pendingToolDepletedSignalCount : PendingToolDepletedCapacity;
                while (scanBudget > 0 && !_pendingToolDepletedSignals.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingToolDepletedSignals.TryDequeue(out _))
                        return true;

                    _pendingToolDepletedSignalCount--;
                    scanBudget--;
                }

                if (_pendingToolDepletedSignals.IsEmpty())
                    _pendingToolDepletedSignalCount = 0;
            }

            return true;
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingTraumaHudSignals.IsCreated && !_pendingTraumaHudSignals.IsEmpty())
                || (_pendingInteractionSignals.IsCreated && !_pendingInteractionSignals.IsEmpty())
                || (_pendingToolDepletedSignals.IsCreated && !_pendingToolDepletedSignals.IsEmpty());
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameTraumaHudSignals.IsCreated)
            {
                while (_nextFrameTraumaHudSignalCount > 0 && _nextFrameTraumaHudSignals.TryDequeue(out TraumaHudSignal signal))
                {
                    _nextFrameTraumaHudSignalCount--;
                    _pendingTraumaHudSignals.Enqueue(signal);
                    _pendingTraumaHudSignalCount++;
                }
            }

            if (_nextFrameInteractionSignals.IsCreated)
            {
                while (_nextFrameInteractionSignalCount > 0 && _nextFrameInteractionSignals.TryDequeue(out InteractionSignal signal))
                {
                    _nextFrameInteractionSignalCount--;
                    _pendingInteractionSignals.Enqueue(signal);
                    _pendingInteractionSignalCount++;
                }
            }

            if (_nextFrameToolDepletedSignals.IsCreated)
            {
                while (_nextFrameToolDepletedSignalCount > 0 && _nextFrameToolDepletedSignals.TryDequeue(out ToolDepletedSignal signal))
                {
                    _nextFrameToolDepletedSignalCount--;
                    _pendingToolDepletedSignals.Enqueue(signal);
                    _pendingToolDepletedSignalCount++;
                }
            }
        }
    }
}
