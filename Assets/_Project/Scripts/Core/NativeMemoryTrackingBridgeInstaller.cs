using Hecton8.Core.Contracts;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Hecton8.Core
{
    internal static class NativeMemoryTrackingBridgeInstaller
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InstallForEditor()
        {
            Install();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            NativeMemoryTrackingBridge.Install(RegisterBytes, RegisterBytesInstance, UnregisterOwnerLabel, UnregisterId);
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

        private static unsafe int RegisterBytesInstance(
            long bytes,
            string owner,
            string label,
            NativeMemoryBridgeLifetime lifetime)
        {
            return NativeMemorySentinel.RegisterPointerlessBridgeRecord(
                bytes,
                owner,
                label,
                ConvertLifetime(lifetime));
        }

        private static void UnregisterOwnerLabel(string owner, string label)
        {
            NativeMemorySentinel.Unregister(owner, label);
        }

        private static void UnregisterId(int id)
        {
            NativeMemorySentinel.Unregister(id);
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
