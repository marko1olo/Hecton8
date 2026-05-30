using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physics
{
    /// <summary>
    /// Cold validator for tether black-box rings.
    /// The authoritative telemetry stays in the owner NativeArray; runtime disk serialization is disabled.
    /// </summary>
    internal static class TetherBlackBoxDumpWriter
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForSubsystemReload()
        {
        }

        public static void WritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            TryWritePrimaryAndLegacy(primaryH8DumpPath, legacyBinPath, magic, ring, head, reasonFlags);
        }

        public static bool TryWritePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            _ = primaryH8DumpPath;
            _ = legacyBinPath;
            _ = magic;
            _ = reasonFlags;

            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            int recordBytes = UnsafeUtility.SizeOf<T>();
            if (recordBytes <= 0)
                return false;

            int count = ring.Length;
            if (head < 0 || head >= count)
                head = 0;

            long bytes = (long)count * recordBytes;
            if (bytes <= 0 || bytes > int.MaxValue)
                return false;

            _ = ring[head];
            return true;
        }

        public static bool TryQueuePrimaryAndLegacy<T>(
            string primaryH8DumpPath,
            string legacyBinPath,
            ulong magic,
            NativeArray<T> ring,
            int head,
            uint reasonFlags) where T : unmanaged
        {
            return TryWritePrimaryAndLegacy(primaryH8DumpPath, legacyBinPath, magic, ring, head, reasonFlags);
        }
    }
}
