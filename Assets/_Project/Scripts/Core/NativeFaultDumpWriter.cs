using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Compatibility facade for fixed native fault payloads. Callers stage bytes in native memory;
    /// this bridge submits the exact span to the native save IO layer.
    /// </summary>
    public static class NativeFaultDumpWriter
    {
        public static unsafe bool TryWriteAll(string absolutePath, NativeArray<byte> payload, int byteCount)
        {
            if (string.IsNullOrEmpty(absolutePath) || !payload.IsCreated || byteCount <= 0 || byteCount > payload.Length)
            {
                return false;
            }

            void* payloadPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload);
            return AsyncWriteManager.WriteAll(absolutePath, payloadPtr, byteCount, out _);
        }
    }
}
