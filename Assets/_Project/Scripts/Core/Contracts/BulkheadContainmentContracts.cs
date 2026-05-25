using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    internal static class BulkheadContainmentContractLayout
    {
        public const int IntentStrideBytes = 64;
        public const int IntentControlStrideBytes = 64;
        public const int CollisionResultStrideBytes = 32;
    }

    public static class BulkheadContainmentIntentFlags
    {
        public const uint None = 0u;
        public const uint Locked = 1u << 0;
        public const uint Valid = 1u << 1;
        public const uint OverflowCompensated = 1u << 2;
        public const uint NonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadContainmentContractLayout.IntentStrideBytes)]
    public struct BulkheadContainmentIntentDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float WidthMeters;
        [FieldOffset(40)] public float HeightMeters;
        [FieldOffset(44)] public float ParentIntegrity01;
        [FieldOffset(48)] public uint EdgeHashID;
        [FieldOffset(52)] public uint SiblingNodeHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadContainmentContractLayout.IntentControlStrideBytes)]
    public struct BulkheadContainmentIntentControlDTO
    {
        [FieldOffset(0)] public uint WriteCursor;
        [FieldOffset(4)] public uint ReadCursor;
        [FieldOffset(8)] public uint Capacity;
        [FieldOffset(12)] public uint Dropped;
        [FieldOffset(16)] public uint LastEdgeHashID;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
        [FieldOffset(32)] public ulong Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    public static class BulkheadCollisionFlags
    {
        public const uint None = 0u;
        public const uint Blocked = 1u << 0;
        public const uint Jammed = 1u << 1;
        public const uint Destroyed = 1u << 2;
        public const uint NonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadContainmentContractLayout.CollisionResultStrideBytes)]
    public struct BulkheadCollisionResultDTO
    {
        [FieldOffset(0)] public float3 Normal;
        [FieldOffset(12)] public float DepthMeters;
        [FieldOffset(16)] public uint EdgeHashID;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float ClosureProgress;
        [FieldOffset(28)] public uint Frame;
    }
}
