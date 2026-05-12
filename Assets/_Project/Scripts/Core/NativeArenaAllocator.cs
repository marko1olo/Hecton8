using Unity.Collections;

namespace Hecton8.Core
{
    /// <summary>
    /// Legacy compatibility shim for older callers. The authoritative storage is <see cref="HectonArenaAllocator"/>.
    /// </summary>
    internal static unsafe class NativeArenaAllocator
    {
        private const int DefaultArenaBytes = 256 * 1024;

        public static int CapacityBytes => HectonArenaAllocator.WriteCapacityBytes;
        public static int UsedBytes => HectonArenaAllocator.UsedBytes;

        public static void Initialize(int capacityBytes = DefaultArenaBytes)
        {
            int resolvedCapacity = capacityBytes <= DefaultArenaBytes
                ? HectonArenaAllocator.DefaultArenaBytes
                : capacityBytes;
            HectonArenaAllocator.Initialize(resolvedCapacity);
        }

        public static NativeArray<T> Allocate<T>(int count) where T : unmanaged
        {
            return HectonArenaAllocator.Allocate<T>(count);
        }

        internal static bool TryAllocateBytes(int byteCount, int alignment, out byte* ptr)
        {
            return HectonArenaAllocator.TryAllocateBytes(byteCount, alignment, out ptr);
        }

        public static void Reset()
        {
            HectonArenaAllocator.EndFrameSwap();
        }

        public static void Shutdown()
        {
            HectonArenaAllocator.Shutdown();
        }
    }
}
