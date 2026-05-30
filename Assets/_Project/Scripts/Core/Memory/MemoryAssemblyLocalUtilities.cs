using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Hecton8.Core.Memory
{
    internal static unsafe class MemoryUnsafeCopyGuard
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeCopy(void* destination, long destinationSizeBytes, void* source, long sourceSizeBytes)
        {
            return Hecton8.Core.UnsafeMemoryCopyGuard.SafeCopy(destination, destinationSizeBytes, source, sourceSizeBytes);
        }
    }

    internal static class MemoryFaultDumpWriter
    {
        public static NativeArray<byte> CreateTransientPayload(
            int byteCount,
            string owner,
            string label,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory,
            Allocator allocator = Allocator.Temp)
        {
            return Hecton8.Core.NativeFaultDumpWriter.CreateTransientPayload(byteCount, owner, label, options, allocator);
        }

        public static void DisposeTransientPayload(
            ref NativeArray<byte> payload,
            string owner,
            string label,
            Allocator allocator = Allocator.Temp)
        {
            Hecton8.Core.NativeFaultDumpWriter.DisposeTransientPayload(ref payload, owner, label, allocator);
        }

        public static bool TryWriteAll(string path, NativeArray<byte> payload, int byteCount)
        {
            return Hecton8.Core.NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
        }
    }
}
