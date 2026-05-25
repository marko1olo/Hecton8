using System.Runtime.InteropServices;

namespace Hecton8.Core
{
    /// <summary>
    /// Legacy compatibility shim for callers that still request raw byte blocks from the arena.
    /// </summary>
    internal static unsafe class UnsafeArenaAllocator
    {
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        internal readonly struct ArenaBlock
        {
            [FieldOffset(0)]
            internal readonly byte* Ptr;

            [FieldOffset(8)]
            internal readonly int ByteCount;

            [FieldOffset(12)]
            private readonly int _pad0;

            internal ArenaBlock(byte* ptr, int byteCount)
            {
                Ptr = ptr;
                ByteCount = byteCount;
                _pad0 = 0;
            }
        }

        private const int DefaultArenaBytes = 256 * 1024;

        internal static int CapacityBytes => NativeArenaAllocator.CapacityBytes;
        internal static int UsedBytes => NativeArenaAllocator.UsedBytes;

        internal static void Initialize(int capacityBytes = DefaultArenaBytes)
        {
            NativeArenaAllocator.Initialize(capacityBytes);
        }

        internal static bool TryAllocate(int byteCount, int alignment, out ArenaBlock block)
        {
            block = default;
            if (!NativeArenaAllocator.TryAllocateBytes(byteCount, alignment, out byte* ptr))
                return false;

            block = new ArenaBlock(ptr, byteCount);
            return true;
        }

        internal static void ResetFrame()
        {
            NativeArenaAllocator.Reset();
        }

        internal static void Shutdown()
        {
            NativeArenaAllocator.Shutdown();
        }
    }
}
