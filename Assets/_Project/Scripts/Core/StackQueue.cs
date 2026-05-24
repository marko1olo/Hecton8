using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed 256-byte FIFO for same-step transient events. No heap ownership, no disposal.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct StackQueue<T> where T : unmanaged
    {
        private const int BufferBytes = 256;
        private const int AlignmentBytes = 16;

        private fixed byte Buffer[BufferBytes];
        private ushort _head;
        private ushort _tail;
        private ushort _count;
        private ushort _capacity;
        private ushort _mask;
        private ushort _pad0;
        private uint _pad1;

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

                return ComputeCapacityPowerOfTwo(elementSize);
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

            _tail = (ushort)((_tail + 1) & _mask);
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
            }

            _head = (ushort)((_head + 1) & _mask);
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

            int capacity = ComputeCapacityPowerOfTwo(elementSize);
            _capacity = (ushort)capacity;
            _mask = capacity > 0 ? (ushort)(capacity - 1) : (ushort)0;
            return capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeCapacityPowerOfTwo(int elementSize)
        {
            fixed (byte* raw = Buffer)
            {
                byte* aligned = Align(raw);
                int usableBytes = BufferBytes - (int)(aligned - raw);
                int rawCapacity = usableBytes > 0 ? usableBytes / elementSize : 0;
                return FloorPowerOfTwo(rawCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorPowerOfTwo(int value)
        {
            if (value <= 0)
                return 0;

            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value - (value >> 1);
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
