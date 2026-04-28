using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed-capacity unsafe arena for zero-GC transient scratch lanes that must not use Allocator.Temp in runtime hot paths.
    /// </summary>
    internal static unsafe class UnsafeArenaAllocator
    {
        internal readonly struct ArenaBlock
        {
            public readonly byte* Ptr;
            public readonly int ByteCount;

            public ArenaBlock(byte* ptr, int byteCount)
            {
                Ptr = ptr;
                ByteCount = byteCount;
            }
        }

        private const int DefaultArenaBytes = 256 * 1024;

        private static readonly ProfilerMarker _resetProfilerMarker = new ProfilerMarker("H8.Core.UnsafeArena.Reset");

        private static byte* _basePtr;
        private static int _capacityBytes;
        private static int _cursorBytes;

        public static int CapacityBytes => _capacityBytes;
        public static int UsedBytes => _cursorBytes;

        public static void Initialize(int capacityBytes = DefaultArenaBytes)
        {
            if (_basePtr != null)
                return;

            _capacityBytes = Math.Max(1024, capacityBytes);
            _basePtr = (byte*)UnsafeUtility.Malloc(_capacityBytes, 16, Unity.Collections.Allocator.Persistent);
            UnsafeUtility.MemClear(_basePtr, _capacityBytes);
            _cursorBytes = 0;
        }

        public static bool TryAllocate(int byteCount, int alignment, out ArenaBlock block)
        {
            block = default;
            if (byteCount <= 0)
                return false;

            Initialize();
            int safeAlignment = Math.Max(1, alignment);
            long alignedAddress = ((long)_basePtr + _cursorBytes + (safeAlignment - 1)) & ~((long)safeAlignment - 1);
            int alignedOffset = (int)(alignedAddress - (long)_basePtr);
            int nextCursor = alignedOffset + byteCount;
            if (nextCursor > _capacityBytes)
                return false;

            byte* ptr = _basePtr + alignedOffset;
            _cursorBytes = nextCursor;
            block = new ArenaBlock(ptr, byteCount);
            return true;
        }

        public static void ResetFrame()
        {
            if (_basePtr == null)
                return;

            using (_resetProfilerMarker.Auto())
            {
                _cursorBytes = 0;
            }
        }

        public static void Shutdown()
        {
            if (_basePtr == null)
                return;

            UnsafeUtility.Free(_basePtr, Unity.Collections.Allocator.Persistent);
            _basePtr = null;
            _capacityBytes = 0;
            _cursorBytes = 0;
        }
    }
}
