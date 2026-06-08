using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class VehicleCommandSignalLayout
    {
        internal const int SignalStrideBytes = 32;
    }

    [System.Flags]
    public enum VehicleCommandSignalFlags : byte
    {
        None = 0,
        ManualPitch = 1 << 0,
        ManualYaw = 1 << 1,
        ManualThrottle = 1 << 2,
        BallastBlow = 1 << 3,
        TowLoadLimit = 1 << 4,
        CriticalList = 1 << 5
    }

    [StructLayout(LayoutKind.Explicit, Size = VehicleCommandSignalLayout.SignalStrideBytes)]
    public struct VehicleCommandSignal : ISignal
    {
        [FieldOffset(0)] public int TargetInstanceId;
        [FieldOffset(4)] public float Pitch;
        [FieldOffset(8)] public float Yaw;
        [FieldOffset(12)] public float Throttle;
        [FieldOffset(16)] public float BallastDelta;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private ushort _pad1;
        [FieldOffset(28)] private int _pad2;
    }

    public interface IVehicleCommandSignalListener
    {
        void OnVehicleCommandSignal(in VehicleCommandSignal signal);
    }
}

namespace Hecton8.Core
{
    /// <summary>
    /// Native queued command lane between input-facing transport owners and vehicle-domain controllers.
    /// </summary>
    public static class VehicleCommandSignalBus
    {
        private const int PendingCommandCapacity = 32;
        private const int ListenerCapacity = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IVehicleCommandSignalListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct VehicleCommandListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public VehicleCommandListenerRegistry(int capacity)
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

            public bool Contains(IVehicleCommandSignalListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IVehicleCommandSignalListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IVehicleCommandSignalListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return true;
                }

                return false;
            }

            public IVehicleCommandSignalListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - vehicle command listeners - owner: VehicleCommandSignalBus
        private static VehicleCommandListenerRegistry _listeners = new VehicleCommandListenerRegistry(ListenerCapacity);

        private static NativeQueue<VehicleCommandSignal> _pendingCommands;
        private static NativeQueue<VehicleCommandSignal> _nextFrameCommands;
        private static int _pendingCommandsSentinelId;
        private static int _nextFrameCommandsSentinelId;
        private static int _pendingCommandCount;
        private static int _nextFrameCommandCount;
        private static int _droppedCommandCount;
        private static uint _nextSequence;
        private static bool _isDispatching;

        // BRIDGE CONTRACT: owner: VehicleCommandSignalBus; drain phase: SystemDispatcher LateUpdate/VISUAL_SYNC flush;
        // max frame budget/capacity: PendingCommandCapacity; overflow policy: fail-fast/drop newest via false return, sequence preserves deterministic FIFO, next-frame prevents same-frame reentrancy; telemetry counter: DroppedCount.

        public static int DroppedCount => _droppedCommandCount;

        public static void Register(IVehicleCommandSignalListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.TryRegister(listener);
        }

        public static void Unregister(IVehicleCommandSignalListener listener)
        {
            if (listener == null)
                return;

            _listeners.TryUnregister(listener);
        }

        [global::System.Obsolete("Use TryPublish and handle bounded vehicle command rejection explicitly.", true)]
        public static bool Publish(in VehicleCommandSignal signal)
        {
            return TryPublish(in signal);
        }

        public static bool TryPublish(in VehicleCommandSignal signal)
        {
            if (signal.TargetInstanceId == 0)
                return false;

            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingCommandCount + _nextFrameCommandCount >= PendingCommandCapacity)
            {
                _droppedCommandCount++;
                return false;
            }

            VehicleCommandSignal queued = signal;
            queued.Sequence = ResolveNextSequence();
            if (_isDispatching)
            {
                _nextFrameCommands.Enqueue(queued);
                _nextFrameCommandCount++;
            }
            else
            {
                _pendingCommands.Enqueue(queued);
                _pendingCommandCount++;
            }

            return true;
        }

        public static void FlushPending()
        {
            if (!_pendingCommands.IsCreated || _pendingCommandCount <= 0)
            {
                PromoteNextFrameCommands();
                return;
            }

            _isDispatching = true;
            try
            {
                while (_pendingCommandCount > 0 && _pendingCommands.TryDequeue(out VehicleCommandSignal signal))
                {
                    _pendingCommandCount--;
                    int count = _listeners.Count;
                    for (int i = 0; i < count; i++)
                    {
                        IVehicleCommandSignalListener listener = _listeners.GetAt(i);
                        if (listener != null)
                            listener.OnVehicleCommandSignal(in signal);
                    }
                }
            }
            finally
            {
                _isDispatching = false;
                PromoteNextFrameCommands();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeQueue(ref _pendingCommands, ref _pendingCommandsSentinelId);
            DisposeQueue(ref _nextFrameCommands, ref _nextFrameCommandsSentinelId);
            _pendingCommandCount = 0;
            _nextFrameCommandCount = 0;
            _droppedCommandCount = 0;
            _nextSequence = 0u;
            _isDispatching = false;
            _listeners.Clear();
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingCommands.IsCreated)
                {
                    _pendingCommands = new NativeQueue<VehicleCommandSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<VehicleCommandSignal>[32] - fixed vehicle command ingress lane - owner: VehicleCommandSignalBus
                    RegisterNativeQueue(ref _pendingCommands, PendingCommandCapacity, nameof(_pendingCommands), out _pendingCommandsSentinelId);
                    PrewarmQueue(ref _pendingCommands, PendingCommandCapacity);
                }

                if (!_nextFrameCommands.IsCreated)
                {
                    _nextFrameCommands = new NativeQueue<VehicleCommandSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<VehicleCommandSignal>[32] - next-frame command lane for reentrant publishes - owner: VehicleCommandSignalBus
                    RegisterNativeQueue(ref _nextFrameCommands, PendingCommandCapacity, nameof(_nextFrameCommands), out _nextFrameCommandsSentinelId);
                    PrewarmQueue(ref _nextFrameCommands, PendingCommandCapacity);
                }
            }
            catch
            {
                DisposeQueue(ref _pendingCommands, ref _pendingCommandsSentinelId);
                DisposeQueue(ref _nextFrameCommands, ref _nextFrameCommandsSentinelId);
                _pendingCommandCount = 0;
                _nextFrameCommandCount = 0;
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
                nameof(VehicleCommandSignalBus),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            DisposeQueue(ref queue, ref sentinelId);
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static uint ResolveNextSequence()
        {
            _nextSequence++;
            if (_nextSequence == 0u)
                _nextSequence = 1u;
            return _nextSequence;
        }

        private static void PromoteNextFrameCommands()
        {
            if (!_nextFrameCommands.IsCreated || _nextFrameCommandCount <= 0)
                return;

            NativeQueue<VehicleCommandSignal> oldPending = _pendingCommands;
            _pendingCommands = _nextFrameCommands;
            _nextFrameCommands = oldPending;
            int sentinelIdSwap = _pendingCommandsSentinelId;
            _pendingCommandsSentinelId = _nextFrameCommandsSentinelId;
            _nextFrameCommandsSentinelId = sentinelIdSwap;
            _pendingCommandCount = _nextFrameCommandCount;
            _nextFrameCommandCount = 0;
        }

        private static void PrewarmQueue(ref NativeQueue<VehicleCommandSignal> queue, int capacity)
        {
            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            for (int i = 0; i < capacity; i++)
                queue.TryDequeue(out _);
        }

        private static void DisposeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
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
    }
}
