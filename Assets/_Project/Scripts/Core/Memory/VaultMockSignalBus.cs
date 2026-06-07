#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Local memory-assembly relocation signal used when the global signal lane is unavailable. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultMemoryAddressShiftSignal
    {
        [FieldOffset(0)] public long OldOffsetBytes;
        [FieldOffset(8)] public long NewOffsetBytes;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteLength;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte SystemId;
        [FieldOffset(30)] private ushort _pad0;
        [FieldOffset(32)] public int OldIndex;
        [FieldOffset(36)] public int NewIndex;
        [FieldOffset(40)] public uint MovedEntityId;
        [FieldOffset(44)] public uint SourceFrame;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint CompactedCount;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>
    /// Cold-test NativeQueue signal lane so vault algorithms do not depend on peer agents.
    /// </summary>
    public struct MockSignalBus<T> : IDisposable where T : unmanaged
    {
        private const int MinimumExpectedCapacity = 1;
        private const string NativeMemoryOwner = "MockSignalBus";
        private const string QueueLabel = "_queue";

        private NativeQueue<T> _queue;
        private int _sentinelRegistrationId;

        /// <summary>Creates the local queue.</summary>
        public MockSignalBus(Allocator allocator, int expectedCapacity = MinimumExpectedCapacity)
        {
            int capacity = Math.Max(MinimumExpectedCapacity, expectedCapacity);
            _queue = new NativeQueue<T>(allocator);
            _sentinelRegistrationId = NativeMemorySentinel.RegisterNativeQueue(
                _queue,
                capacity,
                NativeMemoryOwner,
                QueueLabel,
                ToNativeAllocationLifetime(allocator));
            if (_sentinelRegistrationId <= 0)
            {
                _queue.Dispose();
                _queue = default;
                throw new InvalidOperationException($"Native memory sentinel registration failed for {QueueLabel}.");
            }

            PrewarmQueue(ref _queue, capacity);
        }

        /// <summary>True when the queue is allocated.</summary>
        public bool IsCreated => _queue.IsCreated;

        /// <summary>Queues a signal for local tests.</summary>
        public void Enqueue(in T signal)
        {
            if (!_queue.IsCreated)
                return;

            _queue.Enqueue(signal);
        }

        /// <summary>Attempts to dequeue one local test signal.</summary>
        public bool TryDequeue(out T signal)
        {
            signal = default;
            return _queue.IsCreated && _queue.TryDequeue(out signal);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_queue.IsCreated)
            {
                NativeMemorySentinel.Unregister(_sentinelRegistrationId);
                _sentinelRegistrationId = 0;
                _queue.Dispose();
            }
        }

        private static NativeAllocationLifetime ToNativeAllocationLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                default:
                    return NativeAllocationLifetime.Temp;
            }
        }

        private static void PrewarmQueue(ref NativeQueue<T> queue, int capacity)
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }
    }
}
#endif
