#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Local memory-assembly relocation signal used when the global signal lane is unavailable. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultMemoryAddressShiftSignal
    {
        [FieldOffset(0)] public long OldPointer;
        [FieldOffset(8)] public long NewPointer;
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
        private NativeQueue<T> _queue;

        /// <summary>Creates the local queue.</summary>
        public MockSignalBus(Allocator allocator)
        {
            _queue = new NativeQueue<T>(allocator);
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
                _queue.Dispose();
        }
    }
}
#endif
