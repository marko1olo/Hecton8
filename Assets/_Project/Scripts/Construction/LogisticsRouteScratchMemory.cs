using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Persistent scratch memory for logistics CSR routing.
    /// Owner-only mutable buffers; external callers route through BaseLogisticsNetwork.
    /// </summary>
    internal static class LogisticsRouteScratchMemory
    {
        private const int InitialNodeCapacity = 4096;
        private const int InitialEdgeCapacity = 8192;
        private const string NativeMemoryOwner = nameof(LogisticsRouteScratchMemory);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        internal static NativeArray<int> EdgeOffsets;
        internal static NativeArray<int> EdgeDestinations;
        internal static NativeArray<int> EdgeWriteCursor;
        internal static NativeArray<byte> StorageCapacityByNode;
        internal static NativeArray<byte> Visited;
        internal static NativeArray<int> Queue;
        internal static NativeArray<int> ResultNodeIndex;

        internal static void EnsureCapacity(int nodeCount, int edgeCount)
        {
            EnsureNativeIntArray(ref EdgeOffsets, math.max(InitialNodeCapacity + 1, nodeCount + 1), nameof(EdgeOffsets));
            EnsureNativeIntArray(ref EdgeDestinations, math.max(InitialEdgeCapacity, edgeCount), nameof(EdgeDestinations));
            EnsureNativeIntArray(ref EdgeWriteCursor, math.max(InitialNodeCapacity, nodeCount), nameof(EdgeWriteCursor));
            EnsureNativeByteArray(ref StorageCapacityByNode, math.max(InitialNodeCapacity, nodeCount), nameof(StorageCapacityByNode));
            EnsureNativeByteArray(ref Visited, math.max(InitialNodeCapacity, nodeCount), nameof(Visited));
            EnsureNativeIntArray(ref Queue, math.max(InitialNodeCapacity, nodeCount), nameof(Queue));
            EnsureNativeIntArray(ref ResultNodeIndex, 1, nameof(ResultNodeIndex));
        }

        internal static void Dispose()
        {
            DisposeNativeArray(ref EdgeOffsets);
            DisposeNativeArray(ref EdgeDestinations);
            DisposeNativeArray(ref EdgeWriteCursor);
            DisposeNativeArray(ref StorageCapacityByNode);
            DisposeNativeArray(ref Visited);
            DisposeNativeArray(ref Queue);
            DisposeNativeArray(ref ResultNodeIndex);
        }

        private static void EnsureNativeIntArray(ref NativeArray<int> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureNativeByteArray(ref NativeArray<byte> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<byte>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }
    }
}
