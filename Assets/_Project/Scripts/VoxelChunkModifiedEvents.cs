using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelChunkModifiedEvent
    {
        [FieldOffset(0)]
        public ulong VolumeInstanceId;
        [FieldOffset(8)]
        public int3 MinAbsoluteCell;
        [FieldOffset(20)]
        public int3 MaxAbsoluteCell;
        [FieldOffset(32)]
        public float VoxelSize;
        [FieldOffset(36)]
        public uint Frame;
        [FieldOffset(40)]
        public byte Operation;
        [FieldOffset(41)]
        public byte Shape;
        [FieldOffset(42)]
        public byte Flags;
        [FieldOffset(43)]
        public byte Reserved0;
        [FieldOffset(44)]
        public uint StateHash;
        [FieldOffset(48)]
        public uint Reserved1;
        [FieldOffset(52)]
        public uint Reserved2;
        [FieldOffset(56)]
        public uint Reserved3;
        [FieldOffset(60)]
        public uint Reserved4;
    }

    public static class VoxelChunkModifiedEvents
    {
        private const int Capacity = 64;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const string NativeOwner = nameof(VoxelChunkModifiedEvents);
        private const string QueueLabel = "VoxelChunkModifiedQueue";

        private static NativeQueue<VoxelChunkModifiedEvent> _events;
        private static int _pendingCount;
        private static int _droppedCount;
        private static int _rejectedCount;
        private static uint _lastDroppedStateHash;
        private static uint _lastRejectedStateHash;

        public static int PendingCount => _pendingCount;
        public static int DebugCapacity => Capacity;
        public static int DebugEventBytes => UnsafeUtility.SizeOf<VoxelChunkModifiedEvent>();
        public static int DebugDroppedCount => _droppedCount;
        public static int DebugRejectedCount => _rejectedCount;
        public static uint DebugLastDroppedStateHash => _lastDroppedStateHash;
        public static uint DebugLastRejectedStateHash => _lastRejectedStateHash;
        public static bool DebugIsValid(in VoxelChunkModifiedEvent evt) => IsValid(in evt);

        [global::System.Obsolete("Use TryPublish and handle bounded queue rejection explicitly.", true)]
        public static void Publish(in VoxelChunkModifiedEvent evt)
        {
            TryPublish(in evt);
        }

        public static bool TryPublish(in VoxelChunkModifiedEvent evt)
        {
            if (!IsValid(in evt))
            {
                _rejectedCount++;
                _lastRejectedStateHash = evt.StateHash;
                return false;
            }

            EnsureInitialized();

            if (_pendingCount >= Capacity && _events.TryDequeue(out VoxelChunkModifiedEvent dropped))
            {
                _pendingCount--;
                _droppedCount++;
                _lastDroppedStateHash = dropped.StateHash;
            }

            if (_pendingCount >= Capacity)
            {
                _rejectedCount++;
                _lastRejectedStateHash = evt.StateHash;
                return false;
            }

            _events.Enqueue(evt);
            _pendingCount++;
            return true;
        }

        public static bool TryDequeue(out VoxelChunkModifiedEvent evt)
        {
            if (!_events.IsCreated)
            {
                evt = default;
                return false;
            }

            bool dequeued = _events.TryDequeue(out evt);
            if (dequeued && _pendingCount > 0)
            {
                _pendingCount--;
            }
            else if (!dequeued)
            {
                _pendingCount = 0;
            }

            return dequeued;
        }

        private static bool IsValid(in VoxelChunkModifiedEvent evt)
        {
            return evt.VolumeInstanceId != 0ul &&
                   math.isfinite(evt.VoxelSize) &&
                   evt.VoxelSize > 0f &&
                   math.all(evt.MinAbsoluteCell <= evt.MaxAbsoluteCell) &&
                   evt.Operation <= (byte)VoxelCarveOperationType.Replace &&
                   evt.Shape <= (byte)VoxelCarveShapeType.Capsule;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterQuitHook()
        {
            Application.quitting -= DisposeAll;
            Application.quitting += DisposeAll;
        }

        private static void EnsureInitialized()
        {
            if (_events.IsCreated)
            {
                return;
            }

            _events = new NativeQueue<VoxelChunkModifiedEvent>(DataVaultExemptSignalLaneAllocator);
            NativeMemorySentinel.RegisterNativeQueue(
                _events,
                Capacity,
                NativeOwner,
                QueueLabel,
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _events, Capacity);
            _pendingCount = 0;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
            {
                return;
            }

            for (int i = 0; i < capacity; i++)
            {
                queue.Enqueue(default);
            }

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void DisposeAll()
        {
            if (!_events.IsCreated)
            {
                _pendingCount = 0;
                _droppedCount = 0;
                _rejectedCount = 0;
                _lastDroppedStateHash = 0u;
                _lastRejectedStateHash = 0u;
                return;
            }

            NativeMemorySentinel.UnregisterNativeQueue(NativeOwner, QueueLabel);
            _events.Dispose();
            _pendingCount = 0;
            _droppedCount = 0;
            _rejectedCount = 0;
            _lastDroppedStateHash = 0u;
            _lastRejectedStateHash = 0u;
        }
    }
}
