#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Local memory-assembly relocation signal used when the global signal lane is unavailable. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VaultMemoryAddressShiftSignal
    {
        public long OldPointer;
        public long NewPointer;
        public int BufferId;
        public int ByteLength;
        public uint Version;
        public byte Flags;
        public byte SystemId;
        private ushort _pad0;
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
