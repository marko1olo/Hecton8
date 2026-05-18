using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Builder-to-preview unmanaged packet. Render owners may consume this without touching PlayerBuilder.
    /// Size: 96 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ConstructionPreviewSignal : ISignal
    {
        public const uint LaneHash = 0x43505256u; // CPRV
        public const byte FlagActive = 1 << 0;
        public const byte FlagFallbackPreview = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float4 Rotation;
        [FieldOffset(64)] public float3 Scale;
        [FieldOffset(76)] public uint ModuleHash;
        [FieldOffset(80)] public uint FailureFlags;
        [FieldOffset(84)] public uint ResultHash;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] public byte IsValid;
        [FieldOffset(93)] public byte Flags;
        [FieldOffset(94)] public ushort _pad0;
    }

    /// <summary>
    /// Construction-owned vegetation exclusion AABB packet. Flora owners may consume it as a typed lane.
    /// Size: 80 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct FloraExclusionSignal : ISignal
    {
        public const uint LaneHash = 0x46455843u; // FEXC
        public const byte OperationApply = 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float3 Extents;
        [FieldOffset(60)] public uint ModuleHash;
        [FieldOffset(64)] public uint SourceEntityLow;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public byte Operation;
        [FieldOffset(73)] public byte Flags;
        [FieldOffset(74)] public ushort _pad0;
        [FieldOffset(76)] public uint _pad1;
    }
}
