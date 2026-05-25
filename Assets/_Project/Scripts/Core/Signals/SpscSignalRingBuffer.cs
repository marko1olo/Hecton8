using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core
{
    /// <summary>Power-of-two single-producer/single-consumer signal fallback using mask wrapping.</summary>
    public struct SpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private Hecton8.Core.Memory.SystemID _owner;
        private int _mask;
        private PaddedSignalIndex _head;
        private PaddedSignalIndex _tail;

        public SpscSignalRingBuffer(int requestedCapacity, Allocator allocator)
            : this(requestedCapacity, allocator, Hecton8.Core.Memory.SystemID.Audio)
        {
        }

        public SpscSignalRingBuffer(int requestedCapacity, Allocator allocator, Hecton8.Core.Memory.SystemID owner)
        {
            int capacity = CeilPowerOfTwo(math.max(2, requestedCapacity + 1));
            if (owner == Hecton8.Core.Memory.SystemID.Unknown)
                owner = Hecton8.Core.Memory.SystemID.Audio;

            _buffer = Hecton8.Core.Memory.H8Memory.Allocate<T>(capacity, owner, allocator, NativeArrayOptions.UninitializedMemory);
            _owner = owner;
            _mask = capacity - 1;
            _head = default;
            _tail = default;
        }

        public bool IsCreated => _buffer.IsCreated;
        public int Capacity => _buffer.IsCreated ? _buffer.Length - 1 : 0;

        public void Dispose()
        {
            if (_buffer.IsCreated)
                Hecton8.Core.Memory.H8Memory.Release(ref _buffer, _owner);

            _buffer = default;
            _owner = Hecton8.Core.Memory.SystemID.Unknown;
            _mask = 0;
            _head = default;
            _tail = default;
        }

        public void Clear()
        {
            Interlocked.Exchange(ref _head.Value, 0);
            Interlocked.Exchange(ref _tail.Value, 0);
        }

        public bool TryEnqueue(in T signal)
        {
            if (!_buffer.IsCreated)
                return false;

            int tail = Volatile.Read(ref _tail.Value);
            int nextTail = (tail + 1) & _mask;
            if (nextTail == Volatile.Read(ref _head.Value))
                return false;

            _buffer[tail] = signal;
            Interlocked.Exchange(ref _tail.Value, nextTail);
            return true;
        }

        public bool TryDequeue(out T signal)
        {
            if (!_buffer.IsCreated)
            {
                signal = default;
                return false;
            }

            int head = Volatile.Read(ref _head.Value);
            if (head == Volatile.Read(ref _tail.Value))
            {
                signal = default;
                return false;
            }

            signal = _buffer[head];
            Interlocked.Exchange(ref _head.Value, (head + 1) & _mask);
            return true;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct PaddedSignalIndex
        {
            [FieldOffset(0)] public int Value;
            [FieldOffset(8)] private ulong _pad0;
            [FieldOffset(16)] private ulong _pad1;
            [FieldOffset(24)] private ulong _pad2;
            [FieldOffset(32)] private ulong _pad3;
            [FieldOffset(40)] private ulong _pad4;
            [FieldOffset(48)] private ulong _pad5;
            [FieldOffset(56)] private ulong _pad6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CeilPowerOfTwo(int value)
        {
            value = math.clamp(value, 2, 1 << 30);
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}
