using Unity.Collections;

namespace Hecton8.Core
{
    /// <summary>
    /// Compatibility facade for fixed native fault payloads. Fault state stays in native owner rings;
    /// runtime disk emission is disabled.
    /// </summary>
    public static class NativeFaultDumpWriter
    {
        public static bool TryWriteAll(string absolutePath, NativeArray<byte> payload, int byteCount)
        {
            _ = absolutePath;
            return payload.IsCreated && byteCount > 0 && byteCount <= payload.Length;
        }
    }
}
