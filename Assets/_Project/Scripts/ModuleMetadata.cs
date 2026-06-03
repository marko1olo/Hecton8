using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
#if UNITY_EDITOR
using Unity.Collections.LowLevel.Unsafe;
#endif
using UnityEngine;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HECTON-8/Building/Module Metadata")]
    public sealed class ModuleMetadata : MonoBehaviour
    {
        [SerializeField] private string moduleStableId = string.Empty;
        [SerializeField] private uint moduleHash;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private ModuleSocketData[] sockets = Array.Empty<ModuleSocketData>();

        public const int ModuleSocketDataStrideBytes = 64;

        public string ModuleStableId => moduleStableId;
        public uint ModuleHash => moduleHash;
        public Bounds LocalBounds => localBounds;
        public int SocketCount => sockets != null ? sockets.Length : 0;
        public ModuleSocketData[] Sockets => sockets ?? Array.Empty<ModuleSocketData>();

        public bool TryGetSocket(int index, out ModuleSocketData socket)
        {
            ModuleSocketData[] source = sockets;
            if (source != null && (uint)index < (uint)source.Length)
            {
                socket = source[index];
                return true;
            }

            socket = default;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureOffline(string stableId, uint stableHash, Bounds bounds, ModuleSocketData[] bakedSockets)
        {
            moduleStableId = stableId ?? string.Empty;
            moduleHash = stableHash;
            localBounds = bounds;
            sockets = bakedSockets ?? Array.Empty<ModuleSocketData>();
        }

        public static bool ValidateSocketDataStride(out int actualStride)
        {
            actualStride = UnsafeUtility.SizeOf<ModuleSocketData>();
            return (actualStride & 7) == 0 && actualStride == ModuleSocketDataStrideBytes;
        }
#endif

        private void OnValidate()
        {
            moduleStableId ??= string.Empty;
            sockets ??= Array.Empty<ModuleSocketData>();

#if UNITY_EDITOR
            if (!ValidateSocketDataStride(out int actualStride))
                Debug.LogError($"{nameof(ModuleSocketData)} stride violation: {actualStride} bytes. Expected {ModuleSocketDataStrideBytes} bytes and 8-byte alignment.", this);
#endif
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ModuleSocketData
        {
            public double AupX;
            public double AupY;
            public double AupZ;
            public Vector3 LocalPosition;
            public Vector3 Forward;
            public uint ConnectorMask;
            public uint StableHash;
            public ushort ModuleId;
            public byte Direction;
            public byte Flags;
            public uint Padding;

            public double3 AupPosition => new double3(AupX, AupY, AupZ);
        }
    }
}
