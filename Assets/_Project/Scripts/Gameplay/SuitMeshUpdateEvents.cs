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

        // COLD ALLOC: RegistryBucket<ISuitMeshUpdateEventListener>[12] - suit mesh listeners - owner: SuitMeshUpdateEvents
        private static readonly RegistryBucket<ISuitMeshUpdateEventListener> _listeners = new RegistryBucket<ISuitMeshUpdateEventListener>(ListenerCapacity);
        private static NativeQueue<SuitMeshUpdateSignal> _pendingSignals;
        private static NativeQueue<SuitMeshUpdateSignal> _nextFrameSignals;
        private static int _pendingSignalCount;
        private static int _nextFrameSignalCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingSignalCount + _nextFrameSignalCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeQueue(ref _pendingSignals, nameof(_pendingSignals));
            DisposeQueue(ref _nextFrameSignals, nameof(_nextFrameSignals));
            _pendingSignalCount = 0;
            _nextFrameSignalCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        public static void Register(ISuitMeshUpdateEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(ISuitMeshUpdateEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void Raise(in SuitMeshUpdateSignal signal)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingSignalCount + _nextFrameSignalCount >= PendingCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameSignals.Enqueue(signal);
                _nextFrameSignalCount++;
            }
            else
            {
                _pendingSignals.Enqueue(signal);
                _pendingSignalCount++;
            }
        }

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
                _pendingSignals = new NativeQueue<SuitMeshUpdateSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SuitMeshUpdateSignal>[16] - deferred suit mesh lane - owner: SuitMeshUpdateEvents
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
                _nextFrameSignals = new NativeQueue<SuitMeshUpdateSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SuitMeshUpdateSignal>[16] - next-frame suit mesh lane - owner: SuitMeshUpdateEvents
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
                    return true;

                _pendingSignalCount--;
                scanBudget--;
                ISuitMeshUpdateEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    ISuitMeshUpdateEventListener listener = rawArray[i];
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
                    return true;

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
