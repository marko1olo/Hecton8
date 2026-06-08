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

        public delegate void UnregisterIdDelegate(int id);

        private static RegisterBytesDelegate s_registerBytes;
        private static RegisterBytesDelegate s_registerBytesInstance;
        private static UnregisterOwnerLabelDelegate s_unregisterOwnerLabel;
        private static UnregisterIdDelegate s_unregisterId;

        public static bool IsInstalled => s_registerBytes != null && s_registerBytesInstance != null && s_unregisterOwnerLabel != null && s_unregisterId != null;

        public static void Install(
            RegisterBytesDelegate registerBytes,
            RegisterBytesDelegate registerBytesInstance,
            UnregisterOwnerLabelDelegate unregisterOwnerLabel,
            UnregisterIdDelegate unregisterId)
        {
            s_registerBytes = registerBytes;
            s_registerBytesInstance = registerBytesInstance;
            s_unregisterOwnerLabel = unregisterOwnerLabel;
            s_unregisterId = unregisterId;
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

        public static int RegisterNativeArrayInstance<T>(
            NativeArray<T> array,
            string owner,
            string label,
            NativeMemoryBridgeLifetime lifetime) where T : struct
        {
            if (!array.IsCreated || s_registerBytesInstance == null)
                return 0;

            long bytes = (long)array.Length * UnsafeUtility.SizeOf<T>();
            return s_registerBytesInstance(bytes, owner, label, lifetime);
        }

        public static int RegisterBytes(
            long bytes,
            string owner,
            string label,
            NativeMemoryBridgeLifetime lifetime)
        {
            if (bytes <= 0 || s_registerBytes == null)
                return 0;

            return s_registerBytes(bytes, owner, label, lifetime);
        }

        public static int RegisterBytesInstance(
            long bytes,
            string owner,
            string label,
            NativeMemoryBridgeLifetime lifetime)
        {
            if (bytes <= 0 || s_registerBytesInstance == null)
                return 0;

            return s_registerBytesInstance(bytes, owner, label, lifetime);
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

        public static void Unregister(int id)
        {
            if (id <= 0 || s_unregisterId == null)
                return;

            s_unregisterId(id);
        }

        public static void Unregister(string owner, string label)
        {
            if (s_unregisterOwnerLabel == null)
                return;

            s_unregisterOwnerLabel(owner, label);
        }
    }
}
