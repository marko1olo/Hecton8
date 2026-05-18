using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
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

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VehicleCommandSignal
    {
        public int TargetInstanceId;
        public float Pitch;
        public float Yaw;
        public float Throttle;
        public float BallastDelta;
        public uint Sequence;
        public byte Flags;
        private byte _pad0;
        private ushort _pad1;
        private int _pad2;
    }

    public interface IVehicleCommandSignalListener
    {
        void OnVehicleCommandSignal(in VehicleCommandSignal signal);
    }

    /// <summary>
    /// Native queued command lane between input-facing transport owners and vehicle-domain controllers.
    /// </summary>
    public static class VehicleCommandSignalBus
    {
        private const int PendingCommandCapacity = 32;
        private const int ListenerCapacity = 16;

        // COLD ALLOC: RegistryBucket<IVehicleCommandSignalListener>[16] - vehicle command listeners - owner: VehicleCommandSignalBus
        private static readonly RegistryBucket<IVehicleCommandSignalListener> _listeners =
            new RegistryBucket<IVehicleCommandSignalListener>(ListenerCapacity);

        private static NativeQueue<VehicleCommandSignal> _pendingCommands;
        private static NativeQueue<VehicleCommandSignal> _nextFrameCommands;
        private static int _pendingCommandCount;
        private static int _nextFrameCommandCount;
        private static uint _nextSequence;
        private static bool _isDispatching;

        public static void Register(IVehicleCommandSignalListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IVehicleCommandSignalListener listener)
        {
            if (listener == null)
                return;

            _listeners.TryUnregister(listener);
        }

        public static bool Publish(in VehicleCommandSignal signal)
        {
            if (signal.TargetInstanceId == 0)
                return false;

            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingCommandCount + _nextFrameCommandCount >= PendingCommandCapacity)
                return false;

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
                    IVehicleCommandSignalListener[] raw = _listeners.RawArray;
                    int count = _listeners.Count;
                    for (int i = 0; i < count; i++)
                    {
                        IVehicleCommandSignalListener listener = raw[i];
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
            DisposeQueue(ref _pendingCommands, nameof(_pendingCommands));
            DisposeQueue(ref _nextFrameCommands, nameof(_nextFrameCommands));
            _pendingCommandCount = 0;
            _nextFrameCommandCount = 0;
            _nextSequence = 0u;
            _isDispatching = false;
            _listeners.Clear();
        }

        private static void EnsureInitialized()
        {
            if (!_pendingCommands.IsCreated)
            {
                _pendingCommands = new NativeQueue<VehicleCommandSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<VehicleCommandSignal>[32] - fixed vehicle command ingress lane - owner: VehicleCommandSignalBus
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingCommands,
                    PendingCommandCapacity,
                    nameof(VehicleCommandSignalBus),
                    nameof(_pendingCommands),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingCommands, PendingCommandCapacity);
            }

            if (!_nextFrameCommands.IsCreated)
            {
                _nextFrameCommands = new NativeQueue<VehicleCommandSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<VehicleCommandSignal>[32] - next-frame command lane for reentrant publishes - owner: VehicleCommandSignalBus
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameCommands,
                    PendingCommandCapacity,
                    nameof(VehicleCommandSignalBus),
                    nameof(_nextFrameCommands),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameCommands, PendingCommandCapacity);
            }
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

        private static void DisposeQueue(ref NativeQueue<VehicleCommandSignal> queue, string label)
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(VehicleCommandSignalBus), label);
            queue.Dispose();
            queue = default;
        }
    }
}
