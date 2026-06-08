using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed-capacity persistent native ring. Writes advance monotonically and wrap over old slots.
    /// </summary>
    /// <typeparam name="T">Blittable payload type stored in contiguous native memory.</typeparam>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct NativeRingBuffer<T> : IDisposable where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private int _capacity;
        private int _indexMask;
        private int _writeGate;
        private long _writeCursor;
        private SystemID _ownerSystem;
        private int _sentinelId;

        /// <summary>
        /// Initializes a fixed-capacity native ring buffer.
        /// </summary>
        /// <param name="capacity">Maximum retained entries.</param>
        /// <param name="allocator">Native allocator used for the backing block.</param>
        /// <param name="options">Native memory clear policy.</param>
        public NativeRingBuffer(int capacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            : this(capacity, allocator, SystemID.CoreDiagnostics, options)
        {
        }

        /// <summary>
        /// Initializes a fixed-capacity native ring buffer with an explicit native-memory owner.
        /// </summary>
        /// <param name="capacity">Maximum retained entries.</param>
        /// <param name="allocator">Native allocator used for the backing block.</param>
        /// <param name="ownerSystem">Recorded H8Memory owner.</param>
        /// <param name="options">Native memory clear policy.</param>
        public NativeRingBuffer(
            int capacity,
            Allocator allocator,
            SystemID ownerSystem,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!IsPowerOfTwo(capacity))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (ownerSystem == SystemID.Unknown)
                throw new ArgumentOutOfRangeException(nameof(ownerSystem));

            _ownerSystem = ownerSystem;
            _buffer = H8Memory.Allocate<T>(capacity, _ownerSystem, allocator, options);
            _capacity = capacity;
            _indexMask = capacity - 1;
            _writeGate = 0;
            _writeCursor = 0L;
            _sentinelId = 0;

            if (!_buffer.IsCreated)
            {
                _capacity = 0;
                _indexMask = -1;
            }
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
        public long TotalWrites => ResolveCommittedWrites(Volatile.Read(ref _writeCursor));

        /// <summary>
        /// Current retained entry count.
        /// </summary>
        public int Count
        {
            get
            {
                if (!_buffer.IsCreated)
                    return 0;

                long writes = ResolveCommittedWrites(Volatile.Read(ref _writeCursor));
                if (writes <= 0L)
                    return 0;

                return writes >= _capacity ? _capacity : (int)writes;
            }
        }

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
            EnterWriteGate();
            try
            {
                if (!_buffer.IsCreated || _capacity <= 0 || _writeCursor == long.MaxValue)
                    return -1;

                long writeIndex = ResolveCommittedWrites(_writeCursor);
                int slot = NormalizeIndex(writeIndex);
                _buffer[slot] = value;
                Volatile.Write(ref _writeCursor, writeIndex + 1L);
                return slot;
            }
            finally
            {
                Volatile.Write(ref _writeGate, 0);
            }
        }

        /// <summary>
        /// Copies a chronological range into a caller-owned destination.
        /// </summary>
        /// <param name="startWriteIndex">First absolute write index to copy.</param>
        /// <param name="totalCount">Number of entries to copy.</param>
        /// <param name="destination">Caller-owned destination buffer.</param>
        public void CopyRange(long startWriteIndex, int totalCount, NativeArray<T> destination)
        {
            TryCopyRange(startWriteIndex, totalCount, destination, 0);
        }

        /// <summary>
        /// Copies a chronological range into a caller-owned destination slice.
        /// </summary>
        /// <param name="startWriteIndex">First absolute write index to copy.</param>
        /// <param name="totalCount">Number of entries to copy.</param>
        /// <param name="destination">Caller-owned destination buffer.</param>
        /// <param name="destinationStartIndex">Destination start slot.</param>
        public void CopyRange(long startWriteIndex, int totalCount, NativeArray<T> destination, int destinationStartIndex)
        {
            TryCopyRange(startWriteIndex, totalCount, destination, destinationStartIndex);
        }

        /// <summary>
        /// Tries to copy a chronological range into a caller-owned destination slice.
        /// </summary>
        /// <param name="startWriteIndex">First absolute write index to copy.</param>
        /// <param name="totalCount">Number of entries to copy.</param>
        /// <param name="destination">Caller-owned destination buffer.</param>
        /// <param name="destinationStartIndex">Destination start slot.</param>
        /// <returns>True when the whole requested range was present and copied.</returns>
        public bool TryCopyRange(long startWriteIndex, int totalCount, NativeArray<T> destination, int destinationStartIndex = 0)
        {
            EnterWriteGate();
            try
            {
                return TryCopyRangeUnsafe(startWriteIndex, totalCount, destination, destinationStartIndex);
            }
            finally
            {
                Volatile.Write(ref _writeGate, 0);
            }
        }

        private bool TryCopyRangeUnsafe(long startWriteIndex, int totalCount, NativeArray<T> destination, int destinationStartIndex)
        {
            int safeCount = totalCount;
            if (!_buffer.IsCreated ||
                !destination.IsCreated ||
                safeCount <= 0 ||
                destinationStartIndex < 0 ||
                destinationStartIndex >= destination.Length)
            {
                return false;
            }

            int destinationCapacity = destination.Length - destinationStartIndex;
            if (safeCount > destinationCapacity)
                safeCount = destinationCapacity;
            if (safeCount > _capacity)
                safeCount = _capacity;
            if (safeCount <= 0)
                return false;

            long committedWrites = ResolveCommittedWrites(_writeCursor);
            long oldestRetained = committedWrites > _capacity ? committedWrites - _capacity : 0L;
            if (committedWrites <= 0L || startWriteIndex < oldestRetained || startWriteIndex >= committedWrites)
            {
                ClearDestinationRange(destination, destinationStartIndex, safeCount);
                return false;
            }

            bool completeRangeCopied = true;
            long availableCount = committedWrites - startWriteIndex;
            if (availableCount < safeCount)
            {
                int validCount = availableCount > 0L ? (int)availableCount : 0;
                ClearDestinationRange(destination, destinationStartIndex + validCount, safeCount - validCount);
                safeCount = validCount;
                completeRangeCopied = false;
                if (safeCount <= 0)
                    return false;
            }

            int sourceIndex = NormalizeIndex(startWriteIndex);
            int firstCopyCount = _capacity - sourceIndex;
            if (firstCopyCount > safeCount)
                firstCopyCount = safeCount;

            NativeArray<T>.Copy(_buffer, sourceIndex, destination, destinationStartIndex, firstCopyCount);

            int remainingCount = safeCount - firstCopyCount;
            if (remainingCount > 0)
                NativeArray<T>.Copy(_buffer, 0, destination, destinationStartIndex + firstCopyCount, remainingCount);

            return completeRangeCopied;
        }

        public void RegisterBackingArray(string owner, string label, NativeAllocationLifetime lifetime)
        {
            if (!_buffer.IsCreated)
                return;
            if (_sentinelId > 0)
                return;

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(_buffer, owner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException($"NativeMemorySentinel rejected native ring backing registration for {owner}.{label}.");

                _sentinelId = sentinelId;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Releases the native backing block.
        /// </summary>
        public unsafe void Dispose()
        {
            if (!_buffer.IsCreated)
                return;

            EnterWriteGate();
            try
            {
                if (_buffer.IsCreated)
                {
                    void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_buffer);
                    int sentinelId = _sentinelId;
                    H8Memory.Release(ref _buffer, _ownerSystem);
                    if (_buffer.IsCreated)
                        return;

                    if (sentinelId > 0)
                    {
                        NativeMemorySentinel.Unregister(sentinelId);
                        _sentinelId = 0;
                    }
                    else
                    {
                        NativeMemorySentinel.UnregisterPointer(trackedPointer);
                    }
                }

                _buffer = default;
                _ownerSystem = default;
                _capacity = 0;
                _indexMask = -1;
                _writeCursor = 0L;
            }
            finally
            {
                Volatile.Write(ref _writeGate, 0);
            }
        }

        private void EnterWriteGate()
        {
            SpinWait spin = default;
            while (Interlocked.CompareExchange(ref _writeGate, 1, 0) != 0)
                spin.SpinOnce();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NormalizeIndex(long index)
        {
            return (int)index & _indexMask;
        }

        private static void ClearDestinationRange(NativeArray<T> destination, int startIndex, int count)
        {
            if (!destination.IsCreated || count <= 0 || startIndex < 0 || startIndex >= destination.Length)
                return;

            int end = startIndex + count;
            if (end > destination.Length || end < startIndex)
                end = destination.Length;

            for (int i = startIndex; i < end; i++)
                destination[i] = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ResolveCommittedWrites(long writes)
        {
            return writes > 0L ? writes : 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOfTwo(int value)
        {
            return (value & (value - 1)) == 0;
        }
    }
}
