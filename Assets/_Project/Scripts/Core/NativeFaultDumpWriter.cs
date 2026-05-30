using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    public static unsafe class NativeFaultDumpWriter
    {
        public static bool TryWriteAll(string absolutePath, NativeArray<byte> payload, int byteCount)
        {
            if (string.IsNullOrEmpty(absolutePath) ||
                !payload.IsCreated ||
                byteCount <= 0 ||
                byteCount > payload.Length)
            {
                return false;
            }

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload);
            return AsyncWriteManager.WriteAll(absolutePath, source, byteCount, out _);
        }
    }
}
