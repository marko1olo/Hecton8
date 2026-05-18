using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Contract-side lifetime mirror for native allocation tracking without a direct Core dependency.
    /// </summary>
    public enum NativeMemoryBridgeLifetime : byte
    {
        Scene = 0,
        Session = 1,
        Permanent = 2,
        TransientArena = 3,
        Temp = 4,
        TempJob = 5
    }

    /// <summary>
    /// Compile-wall-safe bridge for assemblies that can depend on Contracts but not on root Core.
    /// </summary>
    public static class NativeMemoryTrackingBridge
    {
        public delegate int RegisterBytesDelegate(long bytes, string owner, string label, NativeMemoryBridgeLifetime lifetime);

        public delegate void UnregisterOwnerLabelDelegate(string owner, string label);

        private static RegisterBytesDelegate s_registerBytes;
        private static UnregisterOwnerLabelDelegate s_unregisterOwnerLabel;

        public static bool IsInstalled => s_registerBytes != null && s_unregisterOwnerLabel != null;

        public static void Install(RegisterBytesDelegate registerBytes, UnregisterOwnerLabelDelegate unregisterOwnerLabel)
        {
            s_registerBytes = registerBytes;
            s_unregisterOwnerLabel = unregisterOwnerLabel;
        }

        public static int RegisterNativeArray<T>(
            NativeArray<T> array,
            string owner,
            string label,
            NativeMemoryBridgeLifetime lifetime) where T : struct
        {
            if (!array.IsCreated || s_registerBytes == null)
                return 0;

            long bytes = (long)array.Length * UnsafeUtility.SizeOf<T>();
            return s_registerBytes(bytes, owner, label, lifetime);
        }

        public static void UnregisterNativeArray<T>(
            NativeArray<T> array,
            string owner,
            string label) where T : struct
        {
            if (!array.IsCreated || s_unregisterOwnerLabel == null)
                return;

            s_unregisterOwnerLabel(owner, label);
        }
    }
}
