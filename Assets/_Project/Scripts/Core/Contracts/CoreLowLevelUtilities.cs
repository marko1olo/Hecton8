using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core
{
    public sealed class FatalMemoryCorruptionException : Exception
    {
        public FatalMemoryCorruptionException(string message) : base(message)
        {
        }
    }

    public static unsafe class UnsafeMemoryCopyGuard
    {
        private const string RejectedCopyMessage = "[UnsafeMemoryCopyGuard] Rejected unsafe native copy: source bytes exceed destination capacity or pointer/size is invalid.";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeCopy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            if (sourceSizeBytes < 0L || destinationSizeBytes < 0L)
                return RejectInvalidCopy();

            if (sourceSizeBytes == 0L)
                return true;

            if (destination == null || source == null)
                return RejectInvalidCopy();

            if (sourceSizeBytes > destinationSizeBytes)
                return RejectInvalidCopy();

            if (RangesOverlap(destination, source, sourceSizeBytes))
                UnsafeUtility.MemMove(destination, source, sourceSizeBytes);
            else
                UnsafeUtility.MemCpy(destination, source, sourceSizeBytes);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMemCpy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            return SafeCopy(destination, destinationSizeBytes, source, sourceSizeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanCopy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            return destination != null &&
                   source != null &&
                   sourceSizeBytes >= 0L &&
                   destinationSizeBytes >= 0L &&
                   sourceSizeBytes <= destinationSizeBytes;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void ReportRejectedCopy(string owner)
        {
            _ = owner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RejectInvalidCopy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new FatalMemoryCorruptionException(RejectedCopyMessage);
#else
            return false;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RangesOverlap(void* destination, void* source, long byteCount)
        {
            if (byteCount <= 0L || destination == source)
                return destination == source && byteCount > 0L;

            ulong destinationStart = (ulong)destination;
            ulong sourceStart = (ulong)source;
            ulong size = (ulong)byteCount;
            ulong destinationEnd = destinationStart + size;
            ulong sourceEnd = sourceStart + size;

            if (destinationEnd < destinationStart || sourceEnd < sourceStart)
                return true;

            return destinationStart < sourceEnd && sourceStart < destinationEnd;
        }
    }

    public static unsafe class NativeFaultDumpWriter
    {
        public static NativeArray<byte> CreateTransientPayload(
            int byteCount,
            string owner,
            string label,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory,
            Allocator allocator = Allocator.Temp)
        {
            if (byteCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            NativeArray<byte> payload = default;
            bool registered = false;
            try
            {
                payload = new NativeArray<byte>(byteCount, allocator, options);
                registered = TryRegisterTransientPayload(payload, owner, label, allocator);
                return payload;
            }
            catch
            {
                if (registered)
                    TryUnregisterTransientPayload(payload, owner, label);

                if (payload.IsCreated)
                    payload.Dispose();

                throw;
            }
        }

        public static void DisposeTransientPayload(
            ref NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator = Allocator.Temp)
        {
            if (!payload.IsCreated)
                return;

            bool unregistered = TryUnregisterTransientPayload(payload, owner, label);
            try
            {
                payload.Dispose();
                payload = default;
            }
            catch
            {
                if (unregistered && payload.IsCreated)
                    TryRegisterTransientPayload(payload, owner, label, allocator);

                throw;
            }
        }

        private static bool TryRegisterTransientPayload(
            NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator)
        {
            if (!payload.IsCreated || !Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
                return false;

            Hecton8.Core.Contracts.NativeMemoryTrackingBridge.RegisterNativeArray(
                payload,
                owner,
                label,
                ResolveBridgeLifetime(allocator));
            return true;
        }

        private static bool TryUnregisterTransientPayload(
            NativeArray<byte> payload,
            string owner,
            string label)
        {
            if (!payload.IsCreated || !Hecton8.Core.Contracts.NativeMemoryTrackingBridge.IsInstalled)
                return false;

            Hecton8.Core.Contracts.NativeMemoryTrackingBridge.UnregisterNativeArray(payload, owner, label);
            return true;
        }

        private static Hecton8.Core.Contracts.NativeMemoryBridgeLifetime ResolveBridgeLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.Temp;
                case Allocator.TempJob:
                    return Hecton8.Core.Contracts.NativeMemoryBridgeLifetime.TempJob;
                default:
                    throw new ArgumentOutOfRangeException(nameof(allocator), "Fault-dump transient payloads support Temp or TempJob allocators only.");
            }
        }

        public static bool TryWriteAll(string absolutePath, NativeArray<byte> payload, int byteCount)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !payload.IsCreated ||
                byteCount <= 0 ||
                byteCount > payload.Length)
            {
                return false;
            }

            byte[] managed = new byte[byteCount];
            fixed (byte* destination = managed)
            {
                UnsafeUtility.MemCpy(
                    destination,
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload),
                    byteCount);
            }

            return TryWriteAll(absolutePath, managed, byteCount);
        }

        public static bool TryWriteAll(string absolutePath, ReadOnlySpan<byte> payload, int byteCount)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                byteCount <= 0 ||
                byteCount > payload.Length)
            {
                return false;
            }

            string fullPath = Path.GetFullPath(absolutePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            byte[] managed = payload.Slice(0, byteCount).ToArray();
            File.WriteAllBytes(fullPath, managed);
            return true;
        }
    }

    public static class DispatcherJobFence
    {
        private static int _activeSwapWindowDepth;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPreSimulationSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPreSimulationSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginLateFrameSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLateFrameSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPostFixedSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPostFixedSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPostSimulationSwapWindow() => _activeSwapWindowDepth++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPostSimulationSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
            if (!forceComplete && !handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFinalizeCompleted(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }
    }

    public struct MpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private NativeArray<long> _publishedTickets;
        private NativeArray<MpscSignalRingCursorState> _cursor;
        private int _mask;
        private int _capacity;

        public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator)
            : this(requestedCapacity, allocator, default)
        {
        }

        public MpscSignalRingBuffer(int requestedCapacity, Allocator allocator, object owner)
        {
            _ = owner;
            int capacity = CeilPowerOfTwo(math.max(2, requestedCapacity));
            _buffer = new NativeArray<T>(capacity, allocator, NativeArrayOptions.UninitializedMemory);
            _publishedTickets = new NativeArray<long>(capacity, allocator, NativeArrayOptions.ClearMemory);
            _cursor = new NativeArray<MpscSignalRingCursorState>(1, allocator, NativeArrayOptions.ClearMemory);
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
                if (!TryGetCursor(out MpscSignalRingCursorState* cursor))
                    return 0;

                long head = Volatile.Read(ref cursor->Head);
                long tail = Volatile.Read(ref cursor->Tail);
                long count = ResolveCursorDistance(head, tail, _capacity);
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
            if (!IsCreated || !TryGetCursor(out MpscSignalRingCursorState* cursor))
                return false;

            long head = Volatile.Read(ref cursor->Head);
            long tail = Volatile.Read(ref cursor->Tail);
            if (tail <= head || head == long.MaxValue)
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
            if (!TryGetCursor(out MpscSignalRingCursorState* cursor))
                return;

            long tail = Volatile.Read(ref cursor->Tail);
            Interlocked.Exchange(ref cursor->Head, tail);
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
            if (_publishedTickets.IsCreated)
                _publishedTickets.Dispose();
            if (_cursor.IsCreated)
                _cursor.Dispose();

            _buffer = default;
            _publishedTickets = default;
            _cursor = default;
            _mask = 0;
            _capacity = 0;
        }

        private unsafe bool TryGetCursor(out MpscSignalRingCursorState* cursor)
        {
            if (_cursor.IsCreated && _cursor.Length > 0)
            {
                cursor = (MpscSignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                return cursor != null;
            }

            cursor = null;
            return false;
        }

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

        private static long ResolveCursorDistance(long head, long tail, int capacity)
        {
            if (capacity <= 0 || tail <= head)
                return 0L;

            long count = tail - head;
            return count < 0L ? capacity : count;
        }

        public struct ParallelWriter
        {
            [NativeDisableParallelForRestriction] private NativeArray<T> _buffer;
            [NativeDisableParallelForRestriction] private NativeArray<long> _publishedTickets;
            [NativeDisableParallelForRestriction] private NativeArray<MpscSignalRingCursorState> _cursor;
            private int _mask;
            private int _capacity;

            internal ParallelWriter(
                NativeArray<T> buffer,
                NativeArray<long> publishedTickets,
                NativeArray<MpscSignalRingCursorState> cursor,
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

                MpscSignalRingCursorState* cursor = (MpscSignalRingCursorState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_cursor);
                long* published = (long*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_publishedTickets);
                while (true)
                {
                    long head = Volatile.Read(ref cursor->Head);
                    long tail = Volatile.Read(ref cursor->Tail);
                    if (!HasWritableCapacity(head, tail, _capacity))
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

            private static bool HasWritableCapacity(long head, long tail, int capacity)
            {
                if (capacity <= 0 || tail < head || tail == long.MaxValue)
                    return false;

                long count = tail - head;
                return count >= 0L && count < capacity;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct MpscSignalRingCursorState
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
}
