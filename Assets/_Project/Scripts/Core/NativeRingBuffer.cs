using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed-capacity persistent native ring. Writes advance monotonically and wrap over old slots.
    /// </summary>
    /// <typeparam name="T">Blittable payload type stored in contiguous native memory.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRingBuffer<T> : IDisposable where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private int _capacity;
        private int _indexMask;
        private long _writeCursor;

        /// <summary>
        /// Initializes a fixed-capacity native ring buffer.
        /// </summary>
        /// <param name="capacity">Maximum retained entries.</param>
        /// <param name="allocator">Native allocator used for the backing block.</param>
        /// <param name="options">Native memory clear policy.</param>
        public NativeRingBuffer(int capacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!IsPowerOfTwo(capacity))
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new NativeArray<T>(capacity, allocator, (NativeArrayOptions)options);
            _capacity = capacity;
            _indexMask = capacity - 1;
            _writeCursor = 0L;
        }

        /// <summary>
        /// Returns whether the native backing block exists.
        /// </summary>
        public bool IsCreated => _buffer.IsCreated;

        /// <summary>
        /// Fixed maximum entry count.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Number of committed writes since allocation.
        /// </summary>
        public long TotalWrites => Volatile.Read(ref _writeCursor);

        /// <summary>
        /// Current retained entry count.
        /// </summary>
        public int Count
        {
            get
            {
                if (!_buffer.IsCreated)
                    return 0;

                long writes = Volatile.Read(ref _writeCursor);
                if (writes <= 0L)
                    return 0;

                return writes >= _capacity ? _capacity : (int)writes;
            }
        }

        /// <summary>
        /// Exposes the raw native block for sentinel registration and unsafe export.
        /// </summary>
        public NativeArray<T> RawArray => _buffer;

        /// <summary>
        /// Reads or writes the normalized native slot.
        /// </summary>
        /// <param name="index">Raw slot index, not chronological index.</param>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _buffer[index] = value;
        }

        /// <summary>
        /// Writes an entry and returns the overwritten native slot.
        /// </summary>
        /// <param name="value">Entry payload.</param>
        public int Write(in T value)
        {
            long writeIndex = Interlocked.Increment(ref _writeCursor) - 1L;
            int slot = NormalizeIndex(writeIndex);
            _buffer[slot] = value;
            return slot;
        }

        /// <summary>
        /// Copies a chronological range into a caller-owned destination.
        /// </summary>
        /// <param name="startWriteIndex">First absolute write index to copy.</param>
        /// <param name="totalCount">Number of entries to copy.</param>
        /// <param name="destination">Caller-owned destination buffer.</param>
        public void CopyRange(long startWriteIndex, int totalCount, NativeArray<T> destination)
        {
            int safeCount = totalCount;
            if (safeCount > destination.Length)
                safeCount = destination.Length;

            for (int i = 0; i < safeCount; i++)
                destination[i] = _buffer[NormalizeIndex(startWriteIndex + i)];
        }

        /// <summary>
        /// Releases the native backing block.
        /// </summary>
        public void Dispose()
        {
            if (!_buffer.IsCreated)
                return;

            _buffer.Dispose();
            _buffer = default;
            _capacity = 0;
            _indexMask = -1;
            _writeCursor = 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NormalizeIndex(long index)
        {
            return (int)index & _indexMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOfTwo(int value)
        {
            return (value & (value - 1)) == 0;
        }
    }
}
