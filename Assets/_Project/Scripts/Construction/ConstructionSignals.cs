using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    internal static class ConstructionSignalLayout
    {
        public const int PreviewStrideBytes = 128;
        public const int FloraExclusionStrideBytes = 128;
        public const int ExtractorCapacityReachedStrideBytes = 32;
    }

    /// <summary>
    /// Builder-to-preview unmanaged packet. Render owners may consume this without touching PlayerBuilder.
    /// Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ConstructionSignalLayout.PreviewStrideBytes)]
    public struct ConstructionPreviewSignal : ISignal
    {
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x43505256u; // CPRV
        public const byte FlagActive = 1 << 0;
        public const byte FlagFallbackPreview = 1 << 1;
        public const byte FlagSocketSnap = 1 << 2;
        public const byte FlagDearLieActive = 1 << 3;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float4 Rotation;
        [FieldOffset(64)] public float3 Scale;
        [FieldOffset(76)] public uint ModuleHash;
        [FieldOffset(80)] public uint FailureFlags;
        [FieldOffset(84)] public uint ResultHash;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] public byte IsValid;
        [FieldOffset(93)] public byte Flags;
        [FieldOffset(94)] private byte _pad0;
        [FieldOffset(95)] private byte _pad1;
        [FieldOffset(96)] public float DearLieDampen;
        [FieldOffset(100)] public float GlobalQualityWeight;
        [FieldOffset(104)] public float DearLieWiggleSpeed;
        [FieldOffset(108)] private byte _pad2;
        [FieldOffset(109)] private byte _pad3;
        [FieldOffset(110)] private byte _pad4;
        [FieldOffset(111)] private byte _pad5;
        [FieldOffset(112)] private byte _pad6;
        [FieldOffset(113)] private byte _pad7;
        [FieldOffset(114)] private byte _pad8;
        [FieldOffset(115)] private byte _pad9;
        [FieldOffset(116)] private byte _pad10;
        [FieldOffset(117)] private byte _pad11;
        [FieldOffset(118)] private byte _pad12;
        [FieldOffset(119)] private byte _pad13;
        [FieldOffset(120)] private byte _pad14;
        [FieldOffset(121)] private byte _pad15;
        [FieldOffset(122)] private byte _pad16;
        [FieldOffset(123)] private byte _pad17;
        [FieldOffset(124)] private byte _pad18;
        [FieldOffset(125)] private byte _pad19;
        [FieldOffset(126)] private byte _pad20;
        [FieldOffset(127)] private byte _pad21;
    }

    /// <summary>
    /// Construction-owned vegetation exclusion AABB packet. Flora owners may consume it as a typed lane.
    /// Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ConstructionSignalLayout.FloraExclusionStrideBytes)]
    public struct FloraExclusionSignal : ISignal
    {
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x46455843u; // FEXC
        public const byte OperationApply = 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float3 Extents;
        [FieldOffset(60)] public uint ModuleHash;
        [FieldOffset(64)] public uint SourceEntityLow;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public byte Operation;
        [FieldOffset(73)] public byte Flags;
        [FieldOffset(74)] private byte _pad0;
        [FieldOffset(75)] private byte _pad1;
        [FieldOffset(76)] private byte _pad2;
        [FieldOffset(77)] private byte _pad3;
        [FieldOffset(78)] private byte _pad4;
        [FieldOffset(79)] private byte _pad5;
        [FieldOffset(80)] private byte _pad6;
        [FieldOffset(81)] private byte _pad7;
        [FieldOffset(82)] private byte _pad8;
        [FieldOffset(83)] private byte _pad9;
        [FieldOffset(84)] private byte _pad10;
        [FieldOffset(85)] private byte _pad11;
        [FieldOffset(86)] private byte _pad12;
        [FieldOffset(87)] private byte _pad13;
        [FieldOffset(88)] private byte _pad14;
        [FieldOffset(89)] private byte _pad15;
        [FieldOffset(90)] private byte _pad16;
        [FieldOffset(91)] private byte _pad17;
        [FieldOffset(92)] private byte _pad18;
        [FieldOffset(93)] private byte _pad19;
        [FieldOffset(94)] private byte _pad20;
        [FieldOffset(95)] private byte _pad21;
        [FieldOffset(96)] private byte _pad22;
        [FieldOffset(97)] private byte _pad23;
        [FieldOffset(98)] private byte _pad24;
        [FieldOffset(99)] private byte _pad25;
        [FieldOffset(100)] private byte _pad26;
        [FieldOffset(101)] private byte _pad27;
        [FieldOffset(102)] private byte _pad28;
        [FieldOffset(103)] private byte _pad29;
        [FieldOffset(104)] private byte _pad30;
        [FieldOffset(105)] private byte _pad31;
        [FieldOffset(106)] private byte _pad32;
        [FieldOffset(107)] private byte _pad33;
        [FieldOffset(108)] private byte _pad34;
        [FieldOffset(109)] private byte _pad35;
        [FieldOffset(110)] private byte _pad36;
        [FieldOffset(111)] private byte _pad37;
        [FieldOffset(112)] private byte _pad38;
        [FieldOffset(113)] private byte _pad39;
        [FieldOffset(114)] private byte _pad40;
        [FieldOffset(115)] private byte _pad41;
        [FieldOffset(116)] private byte _pad42;
        [FieldOffset(117)] private byte _pad43;
        [FieldOffset(118)] private byte _pad44;
        [FieldOffset(119)] private byte _pad45;
        [FieldOffset(120)] private byte _pad46;
        [FieldOffset(121)] private byte _pad47;
        [FieldOffset(122)] private byte _pad48;
        [FieldOffset(123)] private byte _pad49;
        [FieldOffset(124)] private byte _pad50;
        [FieldOffset(125)] private byte _pad51;
        [FieldOffset(126)] private byte _pad52;
        [FieldOffset(127)] private byte _pad53;
    }

    /// <summary>
    /// Autonomous extractor capacity failure packet. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ConstructionSignalLayout.ExtractorCapacityReachedStrideBytes)]
    public struct ExtractorCapacityReachedSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 4;
        public const int LowTierFrameSignals = 2;
        public const uint LaneHash = 0x58435052u; // XCPR

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int Capacity;
        [FieldOffset(8)] public int ActiveCount;
        [FieldOffset(12)] public int ModuleInstanceId;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint ContextHash;
        [FieldOffset(24)] private ulong _pad0;
    }
}
