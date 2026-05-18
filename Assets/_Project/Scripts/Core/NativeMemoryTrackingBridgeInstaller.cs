using Hecton8.Core.Contracts;
using UnityEngine;

namespace Hecton8.Core
{
    internal static class NativeMemoryTrackingBridgeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            NativeMemoryTrackingBridge.Install(RegisterBytes, NativeMemorySentinel.Unregister);
        }

        private static unsafe int RegisterBytes(
            long bytes,
            string owner,
            string label,
            NativeMemoryBridgeLifetime lifetime)
        {
            return NativeMemorySentinel.RegisterPointer(
                null,
                bytes,
                owner,
                label,
                ConvertLifetime(lifetime));
        }

        private static NativeAllocationLifetime ConvertLifetime(NativeMemoryBridgeLifetime lifetime)
        {
            switch (lifetime)
            {
                case NativeMemoryBridgeLifetime.Scene:
                    return NativeAllocationLifetime.Scene;
                case NativeMemoryBridgeLifetime.Session:
                    return NativeAllocationLifetime.Session;
                case NativeMemoryBridgeLifetime.Permanent:
                    return NativeAllocationLifetime.Permanent;
                case NativeMemoryBridgeLifetime.TransientArena:
                    return NativeAllocationLifetime.TransientArena;
                case NativeMemoryBridgeLifetime.Temp:
                    return NativeAllocationLifetime.Temp;
                case NativeMemoryBridgeLifetime.TempJob:
                    return NativeAllocationLifetime.TempJob;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }
    }
}
