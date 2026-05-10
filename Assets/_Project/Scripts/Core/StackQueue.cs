using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed 256-byte FIFO for same-step transient events. No heap ownership, no disposal.
    /// </summary>
    public unsafe struct StackQueue<T> where T : unmanaged
    {
        private const int BufferBytes = 256;
        private const int AlignmentBytes = 16;

        private fixed byte Buffer[BufferBytes];
        private ushort _head;
        private ushort _tail;
        private ushort _count;
        private ushort _capacity;

        public int Count => _count;
        public bool IsEmpty => _count == 0;
        public bool IsFull => _count >= Capacity;

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int elementSize = UnsafeUtility.SizeOf<T>();
                if (elementSize <= 0 || elementSize > BufferBytes)
                    return 0;

                return ComputeCapacity(elementSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in T value)
        {
            int capacity = EnsureCapacity();
            if (capacity <= 0 || _count >= capacity)
                return false;

            fixed (byte* raw = Buffer)
            {
                T* data = (T*)Align(raw);
                data[_tail] = value;
            }

            int nextTail = _tail + 1;
            _tail = (ushort)(nextTail >= capacity ? 0 : nextTail);
            _count++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T value)
        {
            int capacity = EnsureCapacity();
            if (capacity <= 0 || _count == 0)
            {
                value = default;
                return false;
            }

            fixed (byte* raw = Buffer)
            {
                T* data = (T*)Align(raw);
                value = data[_head];
                data[_head] = default;
            }

            int nextHead = _head + 1;
            _head = (ushort)(nextHead >= capacity ? 0 : nextHead);
            _count--;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T value)
        {
            int capacity = EnsureCapacity();
            if (capacity <= 0 || _count == 0)
            {
                value = default;
                return false;
            }

            fixed (byte* raw = Buffer)
            {
                T* data = (T*)Align(raw);
                value = data[_head];
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int EnsureCapacity()
        {
            if (_capacity != 0)
                return _capacity;

            int elementSize = UnsafeUtility.SizeOf<T>();
            if (elementSize <= 0 || elementSize > BufferBytes)
                return 0;

            int capacity = ComputeCapacity(elementSize);
            _capacity = (ushort)capacity;
            return capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeCapacity(int elementSize)
        {
            fixed (byte* raw = Buffer)
            {
                byte* aligned = Align(raw);
                int usableBytes = BufferBytes - (int)(aligned - raw);
                return usableBytes > 0 ? usableBytes / elementSize : 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte* Align(byte* raw)
        {
            ulong address = (ulong)raw;
            ulong aligned = (address + (AlignmentBytes - 1UL)) & ~(AlignmentBytes - 1UL);
            return (byte*)aligned;
        }
    }
}
