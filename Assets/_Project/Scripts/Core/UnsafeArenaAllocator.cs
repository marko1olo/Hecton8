namespace Hecton8.Core
{
    /// <summary>
    /// Legacy compatibility shim for callers that still request raw byte blocks from the arena.
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

        public static int CapacityBytes => NativeArenaAllocator.CapacityBytes;
        public static int UsedBytes => NativeArenaAllocator.UsedBytes;

        public static void Initialize(int capacityBytes = DefaultArenaBytes)
        {
            NativeArenaAllocator.Initialize(capacityBytes);
        }

        public static bool TryAllocate(int byteCount, int alignment, out ArenaBlock block)
        {
            block = default;
            if (!NativeArenaAllocator.TryAllocateBytes(byteCount, alignment, out byte* ptr))
                return false;

            block = new ArenaBlock(ptr, byteCount);
            return true;
        }

        public static void ResetFrame()
        {
            NativeArenaAllocator.Reset();
        }

        public static void Shutdown()
        {
            NativeArenaAllocator.Shutdown();
        }
    }
}
