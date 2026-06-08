using Hecton8.Core;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// HUD-directed trauma packet raised by runtime damage owners without scene polling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
            BiosRecoveryMode = biosRecoveryMode ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
        }

        [FieldOffset(0)] public readonly float GlitchIntensity;
        [FieldOffset(4)] public readonly float RecoilScalar;
        [FieldOffset(8)] public readonly float TransportPower01;
        [FieldOffset(12)] public readonly float HullIntegrity01;
        [FieldOffset(16)] public readonly byte BiosRecoveryMode;
        [FieldOffset(17)] private readonly byte _pad0;
        [FieldOffset(18)] private readonly ushort _pad1;
        [FieldOffset(20)] private readonly uint _pad2;
        [FieldOffset(24)] private readonly ulong _pad3;
    }

    /// <summary>
    /// Audio-directed internal stress packet for heartbeat / breathing consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct PlayerInteractionStressSignal
    {
        public PlayerInteractionStressSignal(
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

        [FieldOffset(0)] public readonly float Stress01;
        [FieldOffset(4)] public readonly float Volume01;
        [FieldOffset(8)] public readonly float PitchScale;
        [FieldOffset(12)] public readonly float Frequency01;
    }

    /// <summary>
    /// Raised when the equipped tool is exhausted and removed from the inventory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public readonly struct PlayerToolDepletedSignal
    {
        public PlayerToolDepletedSignal(int toolHashId)
        {
            ToolHashId = toolHashId;
            _pad0 = 0;
        }

        [FieldOffset(0)] public readonly int ToolHashId;
        [FieldOffset(4)] private readonly uint _pad0;
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
        void OnInteractionSignal(in PlayerInteractionStressSignal signal);

        /// <summary>Called when an equipped tool was depleted.</summary>
        /// <param name="signal">Tool depletion payload.</param>
        void OnToolDepletedSignal(in PlayerToolDepletedSignal signal);
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
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IPlayerSignalEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct PlayerSignalListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public PlayerSignalListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] - fixed player-signal listener slots drained by SystemDispatcher LateUpdate - owner: PlayerSignalEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IPlayerSignalEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IPlayerSignalEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(IPlayerSignalEventListener listener)
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

            public IPlayerSignalEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static PlayerSignalListenerRegistry _listeners = new PlayerSignalListenerRegistry(ListenerCapacity);
        private static NativeQueue<TraumaHudSignal> _pendingTraumaHudSignals;
        private static NativeQueue<TraumaHudSignal> _nextFrameTraumaHudSignals;
        private static NativeQueue<PlayerInteractionStressSignal> _pendingInteractionSignals;
        private static NativeQueue<PlayerInteractionStressSignal> _nextFrameInteractionSignals;
        private static NativeQueue<PlayerToolDepletedSignal> _pendingToolDepletedSignals;
        private static NativeQueue<PlayerToolDepletedSignal> _nextFrameToolDepletedSignals;
        private static int _pendingTraumaHudSignalsSentinelId;
        private static int _nextFrameTraumaHudSignalsSentinelId;
        private static int _pendingInteractionSignalsSentinelId;
        private static int _nextFrameInteractionSignalsSentinelId;
        private static int _pendingToolDepletedSignalsSentinelId;
        private static int _nextFrameToolDepletedSignalsSentinelId;
        private static int _pendingTraumaHudSignalCount;
        private static int _nextFrameTraumaHudSignalCount;
        private static int _pendingInteractionSignalCount;
        private static int _nextFrameInteractionSignalCount;
        private static int _pendingToolDepletedSignalCount;
        private static int _nextFrameToolDepletedSignalCount;
        private static int _droppedSignalCount;
        private static bool _isDispatching;

        // BRIDGE CONTRACT: owner: PlayerSignalEvents; drain phase: SystemDispatcher LateUpdate/VISUAL_SYNC flush;
        // max frame budget/capacity: fixed per-lane capacities above; overflow policy: fail-fast/drop newest via false return, next-frame prevents same-frame reentrancy; telemetry counter: DroppedCount.

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

        /// <summary>Number of bounded player-signal enqueue refusals since subsystem registration.</summary>
        public static int DroppedCount => _droppedSignalCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _pendingTraumaHudSignalCount = 0;
            _nextFrameTraumaHudSignalCount = 0;
            _pendingInteractionSignalCount = 0;
            _nextFrameInteractionSignalCount = 0;
            _pendingToolDepletedSignalCount = 0;
            _nextFrameToolDepletedSignalCount = 0;
            _droppedSignalCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                ResetStaticState();
        }
#endif

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
                _listeners.TryRegister(listener);
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
        public static bool TryRaiseTraumaHudSignal(in TraumaHudSignal signal)
        {
            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingTraumaHudSignalCount + _nextFrameTraumaHudSignalCount >= PendingTraumaHudCapacity)
            {
                _droppedSignalCount++;
                return false;
            }

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

            return true;
        }

        [Obsolete("Player signal producers must use TryRaiseTraumaHudSignal and handle bounded enqueue failure.", true)]
        public static void RaiseTraumaHudSignal(in TraumaHudSignal signal)
        {
            TryRaiseTraumaHudSignal(in signal);
        }

        /// <summary>
        /// Queues one interaction stress signal.
        /// </summary>
        /// <param name="signal">Signal payload.</param>
        public static bool TryRaiseInteractionSignal(in PlayerInteractionStressSignal signal)
        {
            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingInteractionSignalCount + _nextFrameInteractionSignalCount >= PendingInteractionSignalCapacity)
            {
                _droppedSignalCount++;
                return false;
            }

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

            return true;
        }

        [Obsolete("Player signal producers must use TryRaiseInteractionSignal and handle bounded enqueue failure.", true)]
        public static void RaiseInteractionSignal(in PlayerInteractionStressSignal signal)
        {
            TryRaiseInteractionSignal(in signal);
        }

        /// <summary>
        /// Queues one tool depletion signal.
        /// </summary>
        /// <param name="signal">Signal payload.</param>
        public static bool TryRaiseToolDepletedSignal(in PlayerToolDepletedSignal signal)
        {
            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingToolDepletedSignalCount + _nextFrameToolDepletedSignalCount >= PendingToolDepletedCapacity)
            {
                _droppedSignalCount++;
                return false;
            }

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

            return true;
        }

        [Obsolete("Player signal producers must use TryRaiseToolDepletedSignal and handle bounded enqueue failure.", true)]
        public static void RaiseToolDepletedSignal(in PlayerToolDepletedSignal signal)
        {
            TryRaiseToolDepletedSignal(in signal);
        }

        private static void EnsureInitialized()
        {
            if (!Application.isPlaying)
                return;

            try
            {
                if (!_pendingTraumaHudSignals.IsCreated)
                {
                    _pendingTraumaHudSignals = new NativeQueue<TraumaHudSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<TraumaHudSignal>[16] - deferred trauma HUD lane - owner: PlayerSignalEvents
                    RegisterNativeQueue(ref _pendingTraumaHudSignals, PendingTraumaHudCapacity, nameof(_pendingTraumaHudSignals), out _pendingTraumaHudSignalsSentinelId);
                    PrewarmQueue(ref _pendingTraumaHudSignals, PendingTraumaHudCapacity);
                }
                if (!_nextFrameTraumaHudSignals.IsCreated)
                {
                    _nextFrameTraumaHudSignals = new NativeQueue<TraumaHudSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<TraumaHudSignal>[16] - next-frame trauma HUD lane - owner: PlayerSignalEvents
                    RegisterNativeQueue(ref _nextFrameTraumaHudSignals, PendingTraumaHudCapacity, nameof(_nextFrameTraumaHudSignals), out _nextFrameTraumaHudSignalsSentinelId);
                    PrewarmQueue(ref _nextFrameTraumaHudSignals, PendingTraumaHudCapacity);
                }
                if (!_pendingInteractionSignals.IsCreated)
                {
                    _pendingInteractionSignals = new NativeQueue<PlayerInteractionStressSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PlayerInteractionStressSignal>[16] - deferred interaction stress lane - owner: PlayerSignalEvents
                    RegisterNativeQueue(ref _pendingInteractionSignals, PendingInteractionSignalCapacity, nameof(_pendingInteractionSignals), out _pendingInteractionSignalsSentinelId);
                    PrewarmQueue(ref _pendingInteractionSignals, PendingInteractionSignalCapacity);
                }
                if (!_nextFrameInteractionSignals.IsCreated)
                {
                    _nextFrameInteractionSignals = new NativeQueue<PlayerInteractionStressSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PlayerInteractionStressSignal>[16] - next-frame interaction stress lane - owner: PlayerSignalEvents
                    RegisterNativeQueue(ref _nextFrameInteractionSignals, PendingInteractionSignalCapacity, nameof(_nextFrameInteractionSignals), out _nextFrameInteractionSignalsSentinelId);
                    PrewarmQueue(ref _nextFrameInteractionSignals, PendingInteractionSignalCapacity);
                }
                if (!_pendingToolDepletedSignals.IsCreated)
                {
                    _pendingToolDepletedSignals = new NativeQueue<PlayerToolDepletedSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PlayerToolDepletedSignal>[16] - deferred tool depletion lane - owner: PlayerSignalEvents
                    RegisterNativeQueue(ref _pendingToolDepletedSignals, PendingToolDepletedCapacity, nameof(_pendingToolDepletedSignals), out _pendingToolDepletedSignalsSentinelId);
                    PrewarmQueue(ref _pendingToolDepletedSignals, PendingToolDepletedCapacity);
                }
                if (!_nextFrameToolDepletedSignals.IsCreated)
                {
                    _nextFrameToolDepletedSignals = new NativeQueue<PlayerToolDepletedSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PlayerToolDepletedSignal>[16] - next-frame tool depletion lane - owner: PlayerSignalEvents
                    RegisterNativeQueue(ref _nextFrameToolDepletedSignals, PendingToolDepletedCapacity, nameof(_nextFrameToolDepletedSignals), out _nextFrameToolDepletedSignalsSentinelId);
                    PrewarmQueue(ref _nextFrameToolDepletedSignals, PendingToolDepletedCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingTraumaHudSignalCount = 0;
                _nextFrameTraumaHudSignalCount = 0;
                _pendingInteractionSignalCount = 0;
                _nextFrameInteractionSignalCount = 0;
                _pendingToolDepletedSignalCount = 0;
                _nextFrameToolDepletedSignalCount = 0;
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
                nameof(PlayerSignalEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingTraumaHudSignals, ref _pendingTraumaHudSignalsSentinelId);
            ReleaseNativeQueue(ref _nextFrameTraumaHudSignals, ref _nextFrameTraumaHudSignalsSentinelId);
            ReleaseNativeQueue(ref _pendingInteractionSignals, ref _pendingInteractionSignalsSentinelId);
            ReleaseNativeQueue(ref _nextFrameInteractionSignals, ref _nextFrameInteractionSignalsSentinelId);
            ReleaseNativeQueue(ref _pendingToolDepletedSignals, ref _pendingToolDepletedSignalsSentinelId);
            ReleaseNativeQueue(ref _nextFrameToolDepletedSignals, ref _nextFrameToolDepletedSignalsSentinelId);
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
                {
                    _pendingTraumaHudSignalCount = 0;
                    return true;
                }

                _pendingTraumaHudSignalCount--;
                scanBudget--;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IPlayerSignalEventListener listener = _listeners.GetAt(i);
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

                if (!_pendingInteractionSignals.TryDequeue(out PlayerInteractionStressSignal signal))
                {
                    _pendingInteractionSignalCount = 0;
                    return true;
                }

                _pendingInteractionSignalCount--;
                scanBudget--;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IPlayerSignalEventListener listener = _listeners.GetAt(i);
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

                if (!_pendingToolDepletedSignals.TryDequeue(out PlayerToolDepletedSignal signal))
                {
                    _pendingToolDepletedSignalCount = 0;
                    return true;
                }

                _pendingToolDepletedSignalCount--;
                scanBudget--;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IPlayerSignalEventListener listener = _listeners.GetAt(i);
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
                    {
                        _pendingTraumaHudSignalCount = 0;
                        return true;
                    }

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
                    {
                        _pendingInteractionSignalCount = 0;
                        return true;
                    }

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
                    {
                        _pendingToolDepletedSignalCount = 0;
                        return true;
                    }

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
                while (_nextFrameInteractionSignalCount > 0 && _nextFrameInteractionSignals.TryDequeue(out PlayerInteractionStressSignal signal))
                {
                    _nextFrameInteractionSignalCount--;
                    _pendingInteractionSignals.Enqueue(signal);
                    _pendingInteractionSignalCount++;
                }
            }

            if (_nextFrameToolDepletedSignals.IsCreated)
            {
                while (_nextFrameToolDepletedSignalCount > 0 && _nextFrameToolDepletedSignals.TryDequeue(out PlayerToolDepletedSignal signal))
                {
                    _nextFrameToolDepletedSignalCount--;
                    _pendingToolDepletedSignals.Enqueue(signal);
                    _pendingToolDepletedSignalCount++;
                }
            }
        }
    }
}
