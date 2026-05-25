using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct SignalRingCursorState
    {
        [FieldOffset(0)] public long Head;
        [FieldOffset(8)] private ulong _headPad0;
        [FieldOffset(16)] private ulong _headPad1;
        [FieldOffset(24)] private ulong _headPad2;
        [FieldOffset(32)] private ulong _headPad3;
        [FieldOffset(40)] private ulong _headPad4;
        [FieldOffset(48)] private ulong _headPad5;
        [FieldOffset(56)] private ulong _headPad6;
        [FieldOffset(64)] public long Tail;
        [FieldOffset(72)] private ulong _tailPad0;
        [FieldOffset(80)] private ulong _tailPad1;
        [FieldOffset(88)] private ulong _tailPad2;
        [FieldOffset(96)] private ulong _tailPad3;
        [FieldOffset(104)] private ulong _tailPad4;
        [FieldOffset(112)] private ulong _tailPad5;
        [FieldOffset(120)] private ulong _tailPad6;
    }

    /// <summary>Power-of-two single-producer/single-consumer signal fallback using mask wrapping.</summary>
    public struct SpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private NativeArray<SignalRingCursorState> _cursor;
        private Hecton8.Core.Memory.SystemID _owner;
        private int _mask;

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
            _cursor = Hecton8.Core.Memory.H8Memory.Allocate<SignalRingCursorState>(1, owner, allocator, NativeArrayOptions.ClearMemory);
            _owner = owner;
            _mask = capacity - 1;

            if (!_buffer.IsCreated || !_cursor.IsCreated)
                Dispose();
        }

        public bool IsCreated => _buffer.IsCreated && _cursor.IsCreated;
        public int Capacity => _buffer.IsCreated ? _buffer.Length - 1 : 0;
        public unsafe int Count
        {
            get
            {
                if (!TryGetCursor(out SignalRingCursorState* cursor))
                    return 0;

                long head = Volatile.Read(ref cursor->Head);
                long tail = Volatile.Read(ref cursor->Tail);
                long count = tail - head;
                int capacity = Capacity;
                if (count <= 0L)
                    return 0;
                return count >= capacity ? capacity : (int)count;
            }
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                Hecton8.Core.Memory.H8Memory.Release(ref _buffer, _owner);
            if (_cursor.IsCreated)
                Hecton8.Core.Memory.H8Memory.Release(ref _cursor, _owner);

            _buffer = default;
            _cursor = default;
            _owner = Hecton8.Core.Memory.SystemID.Unknown;
            _mask = 0;
        }

        public unsafe void Clear()
        {
            if (!TryGetCursor(out SignalRingCursorState* cursor))
                return;

            long tail = Volatile.Read(ref cursor->Tail);
            Interlocked.Exchange(ref cursor->Head, tail);
        }

        public unsafe bool TryEnqueue(in T signal)
        {
            if (!_buffer.IsCreated || !TryGetCursor(out SignalRingCursorState* cursor))
                return false;

            long tail = Volatile.Read(ref cursor->Tail);
            long head = Volatile.Read(ref cursor->Head);
            int capacity = Capacity;
            if (tail - head >= capacity)
                return false;

            int slot = (int)tail & _mask;
            _buffer[slot] = signal;
            Interlocked.Exchange(ref cursor->Tail, tail + 1L);
            return true;
        }

        public unsafe bool TryDequeue(out T signal)
        {
            if (!_buffer.IsCreated || !TryGetCursor(out SignalRingCursorState* cursor))
            {
                signal = default;
                return false;
            }

            long head = Volatile.Read(ref cursor->Head);
            long tail = Volatile.Read(ref cursor->Tail);
            if (head == tail)
            {
                signal = default;
                return false;
            }

            int slot = (int)head & _mask;
            signal = _buffer[slot];
            Interlocked.Exchange(ref cursor->Head, head + 1L);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool TryGetCursor(out SignalRingCursorState* cursor)
        {
            if (_cursor.IsCreated && _cursor.Length > 0)
            {
                cursor = (SignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                return cursor != null;
            }

            cursor = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CeilPowerOfTwo(int value)
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

    /// <summary>Bounded multi-producer/single-consumer ring using CAS tail reservation and per-slot publication tickets.</summary>
    public struct MpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private NativeArray<long> _publishedTickets;
        private NativeArray<SignalRingCursorState> _cursor;
        private Hecton8.Core.Memory.SystemID _owner;
        private int _mask;
        private int _capacity;

        public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator)
            : this(requestedCapacity, allocator, Hecton8.Core.Memory.SystemID.CoreDataVault)
        {
        }

        public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator, Hecton8.Core.Memory.SystemID owner)
        {
            int capacity = SpscSignalRingBuffer<T>.CeilPowerOfTwo(math.max(2, requestedCapacity));
            if (owner == Hecton8.Core.Memory.SystemID.Unknown)
                owner = Hecton8.Core.Memory.SystemID.CoreDataVault;

            _buffer = Hecton8.Core.Memory.H8Memory.Allocate<T>(capacity, owner, allocator, NativeArrayOptions.UninitializedMemory);
            _publishedTickets = Hecton8.Core.Memory.H8Memory.Allocate<long>(capacity, owner, allocator, NativeArrayOptions.ClearMemory);
            _cursor = Hecton8.Core.Memory.H8Memory.Allocate<SignalRingCursorState>(1, owner, allocator, NativeArrayOptions.ClearMemory);
            _owner = owner;
            _mask = capacity - 1;
            _capacity = capacity;

            if (!_buffer.IsCreated || !_publishedTickets.IsCreated || !_cursor.IsCreated)
                Dispose();
        }

        public bool IsCreated => _buffer.IsCreated && _publishedTickets.IsCreated && _cursor.IsCreated;
        public int Capacity => _capacity;

        public unsafe int Count
        {
            get
            {
                if (!TryGetCursor(out SignalRingCursorState* cursor))
                    return 0;

                long head = Volatile.Read(ref cursor->Head);
                long tail = Volatile.Read(ref cursor->Tail);
                long count = tail - head;
                if (count <= 0L)
                    return 0;
                return count >= _capacity ? _capacity : (int)count;
            }
        }

        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(_buffer, _publishedTickets, _cursor, _mask, _capacity);
        }

        public bool TryEnqueue(in T signal)
        {
            return AsParallelWriter().TryEnqueue(in signal);
        }

        public unsafe bool TryDequeue(out T signal)
        {
            signal = default;
            if (!IsCreated || !TryGetCursor(out SignalRingCursorState* cursor))
                return false;

            long head = Volatile.Read(ref cursor->Head);
            long tail = Volatile.Read(ref cursor->Tail);
            if (head == tail)
                return false;

            int slot = (int)head & _mask;
            long expectedTicket = head + 1L;
            long* published = (long*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_publishedTickets);
            if (Volatile.Read(ref published[slot]) != expectedTicket)
                return false;

            signal = _buffer[slot];
            Interlocked.Exchange(ref published[slot], 0L);
            Interlocked.Exchange(ref cursor->Head, head + 1L);
            return true;
        }

        public unsafe void Clear()
        {
            if (!TryGetCursor(out SignalRingCursorState* cursor))
                return;

            long tail = Volatile.Read(ref cursor->Tail);
            Interlocked.Exchange(ref cursor->Head, tail);
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                Hecton8.Core.Memory.H8Memory.Release(ref _buffer, _owner);
            if (_publishedTickets.IsCreated)
                Hecton8.Core.Memory.H8Memory.Release(ref _publishedTickets, _owner);
            if (_cursor.IsCreated)
                Hecton8.Core.Memory.H8Memory.Release(ref _cursor, _owner);

            _buffer = default;
            _publishedTickets = default;
            _cursor = default;
            _owner = Hecton8.Core.Memory.SystemID.Unknown;
            _mask = 0;
            _capacity = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool TryGetCursor(out SignalRingCursorState* cursor)
        {
            if (_cursor.IsCreated && _cursor.Length > 0)
            {
                cursor = (SignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                return cursor != null;
            }

            cursor = null;
            return false;
        }

        public struct ParallelWriter
        {
            [NativeDisableParallelForRestriction] private NativeArray<T> _buffer;
            [NativeDisableParallelForRestriction] private NativeArray<long> _publishedTickets;
            [NativeDisableParallelForRestriction] private NativeArray<SignalRingCursorState> _cursor;
            private int _mask;
            private int _capacity;

            internal ParallelWriter(
                NativeArray<T> buffer,
                NativeArray<long> publishedTickets,
                NativeArray<SignalRingCursorState> cursor,
                int mask,
                int capacity)
            {
                _buffer = buffer;
                _publishedTickets = publishedTickets;
                _cursor = cursor;
                _mask = mask;
                _capacity = capacity;
            }

            public bool IsCreated => _buffer.IsCreated && _publishedTickets.IsCreated && _cursor.IsCreated;

            public unsafe bool TryEnqueue(in T signal)
            {
                if (!IsCreated || _capacity <= 0 || _cursor.Length == 0)
                    return false;

                SignalRingCursorState* cursor = (SignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                long* published = (long*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_publishedTickets);
                while (true)
                {
                    long head = Volatile.Read(ref cursor->Head);
                    long tail = Volatile.Read(ref cursor->Tail);
                    if (tail - head >= _capacity)
                        return false;

                    long nextTail = tail + 1L;
                    if (Interlocked.CompareExchange(ref cursor->Tail, nextTail, tail) != tail)
                        continue;

                    int slot = (int)tail & _mask;
                    _buffer[slot] = signal;
                    Interlocked.Exchange(ref published[slot], tail + 1L);
                    return true;
                }
            }
        }
    }
}
