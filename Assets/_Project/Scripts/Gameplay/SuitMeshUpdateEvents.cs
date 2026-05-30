using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct SuitMeshUpdateSignal
    {
        public const uint EmissiveUpgradeFlag = 1u << 0;

        public SuitMeshUpdateSignal(ulong upgradeMask, ulong effectiveUpgradeMask, uint sequence)
        {
            this = default;
            UpgradeMask = upgradeMask;
            EffectiveUpgradeMask = effectiveUpgradeMask;
            Sequence = sequence;
            StatusFlags = effectiveUpgradeMask != 0UL ? EmissiveUpgradeFlag : 0u;
        }

        [FieldOffset(0)] public readonly ulong UpgradeMask;
        [FieldOffset(8)] public readonly ulong EffectiveUpgradeMask;
        [FieldOffset(16)] public readonly uint Sequence;
        [FieldOffset(20)] public readonly uint StatusFlags;
        [FieldOffset(24)] private readonly ulong _pad0;

        public static bool HasEmissiveUpgrade(uint statusFlags)
        {
            return (statusFlags & EmissiveUpgradeFlag) != 0u;
        }
    }

    public interface ISuitMeshUpdateEventListener
    {
        void OnSuitMeshUpdateSignal(in SuitMeshUpdateSignal signal);
    }

    public static class SuitMeshUpdateEvents
    {
        private const int ListenerCapacity = 12;
        private const int PendingCapacity = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public ISuitMeshUpdateEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct SuitMeshUpdateListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public SuitMeshUpdateListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ISuitMeshUpdateEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ISuitMeshUpdateEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(ISuitMeshUpdateEventListener listener)
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

            public ISuitMeshUpdateEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[12] - suit mesh listeners - owner: SuitMeshUpdateEvents
        private static SuitMeshUpdateListenerRegistry _listeners = new SuitMeshUpdateListenerRegistry(ListenerCapacity);
        private static NativeQueue<SuitMeshUpdateSignal> _pendingSignals;
        private static NativeQueue<SuitMeshUpdateSignal> _nextFrameSignals;
        private static int _pendingSignalCount;
        private static int _nextFrameSignalCount;
        private static int _droppedSignalCount;
        private static bool _isDispatching;

        // BRIDGE CONTRACT: owner: SuitMeshUpdateEvents; drain phase: SystemDispatcher LateUpdate/VISUAL_SYNC flush;
        // max frame budget/capacity: PendingCapacity; overflow policy: fail-fast/drop newest via false return, next-frame prevents same-frame reentrancy; telemetry counter: DroppedCount.

        public static int PendingCount => _pendingSignalCount + _nextFrameSignalCount;

        public static int DroppedCount => _droppedSignalCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeQueue(ref _pendingSignals, nameof(_pendingSignals));
            DisposeQueue(ref _nextFrameSignals, nameof(_nextFrameSignals));
            _pendingSignalCount = 0;
            _nextFrameSignalCount = 0;
            _droppedSignalCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        public static void Register(ISuitMeshUpdateEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.TryRegister(listener);
        }

        public static void Unregister(ISuitMeshUpdateEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static bool TryRaise(in SuitMeshUpdateSignal signal)
        {
            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingSignalCount + _nextFrameSignalCount >= PendingCapacity)
            {
                _droppedSignalCount++;
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameSignals.Enqueue(signal);
                _nextFrameSignalCount++;
                return true;
            }

            _pendingSignals.Enqueue(signal);
            _pendingSignalCount++;
            return true;
        }

        [System.Obsolete("Use TryRaise so bounded queue refusal is visible at the producer.", true)]
        public static void Raise(in SuitMeshUpdateSignal signal) => TryRaise(in signal);

        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                completed = _listeners.Count <= 0 ? DrainWithoutDispatch() : FlushSignals();
            }
            finally
            {
                _isDispatching = false;
            }

            if (!completed || (_pendingSignals.IsCreated && !_pendingSignals.IsEmpty()))
                return;

            PromoteNextFrameSignals();
        }

        private static void EnsureInitialized()
        {
            if (!_pendingSignals.IsCreated)
            {
                _pendingSignals = new NativeQueue<SuitMeshUpdateSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SuitMeshUpdateSignal>[16] - deferred suit mesh lane - owner: SuitMeshUpdateEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingSignals,
                    PendingCapacity,
                    nameof(SuitMeshUpdateEvents),
                    nameof(_pendingSignals),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingSignals, PendingCapacity);
            }

            if (!_nextFrameSignals.IsCreated)
            {
                _nextFrameSignals = new NativeQueue<SuitMeshUpdateSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SuitMeshUpdateSignal>[16] - next-frame suit mesh lane - owner: SuitMeshUpdateEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameSignals,
                    PendingCapacity,
                    nameof(SuitMeshUpdateEvents),
                    nameof(_nextFrameSignals),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameSignals, PendingCapacity);
            }
        }

        private static void DisposeQueue(ref NativeQueue<SuitMeshUpdateSignal> queue, string label)
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(SuitMeshUpdateEvents), label);
            queue.Dispose();
            queue = default;
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

        private static bool FlushSignals()
        {
            if (!_pendingSignals.IsCreated)
                return true;

            int scanBudget = _pendingSignalCount > 0 ? _pendingSignalCount : PendingCapacity;
            while (scanBudget > 0 && !_pendingSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSignals.TryDequeue(out SuitMeshUpdateSignal signal))
                {
                    _pendingSignalCount = 0;
                    return true;
                }

                _pendingSignalCount--;
                scanBudget--;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    ISuitMeshUpdateEventListener listener = _listeners.GetAt(i);
                    if (listener != null)
                        listener.OnSuitMeshUpdateSignal(in signal);
                }
            }

            if (_pendingSignals.IsEmpty())
                _pendingSignalCount = 0;

            return true;
        }

        private static bool DrainWithoutDispatch()
        {
            if (!_pendingSignals.IsCreated)
                return true;

            int scanBudget = _pendingSignalCount > 0 ? _pendingSignalCount : PendingCapacity;
            while (scanBudget > 0 && !_pendingSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSignals.TryDequeue(out _))
                {
                    _pendingSignalCount = 0;
                    return true;
                }

                _pendingSignalCount--;
                scanBudget--;
            }

            if (_pendingSignals.IsEmpty())
                _pendingSignalCount = 0;

            return true;
        }

        private static void PromoteNextFrameSignals()
        {
            if (!_nextFrameSignals.IsCreated)
                return;

            EnsureInitialized();
            while (_nextFrameSignalCount > 0 && _nextFrameSignals.TryDequeue(out SuitMeshUpdateSignal signal))
            {
                _nextFrameSignalCount--;
                _pendingSignals.Enqueue(signal);
                _pendingSignalCount++;
            }
        }
    }
}
