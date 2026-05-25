using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class VisualScavengeSignalLayout
    {
        internal const int AupTransferStrideBytes = 64;
        internal const int SignalStrideBytes = 128;
    }

    /// <summary>Contract-local AUP transfer payload for visual scavenging signals. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = VisualScavengeSignalLayout.AupTransferStrideBytes)]
    public struct VisualScavengeAup48
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] public float _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    /// <summary>Visual-only scavenging pickup fake. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = VisualScavengeSignalLayout.SignalStrideBytes)]
    public struct VisualScavengeSignal : ISignal
    {
        [FieldOffset(0)] public VisualScavengeAup48 PositionAup;
        [FieldOffset(64)] public ulong ResourceNodeHash;
        [FieldOffset(72)] public uint ItemHashID;
        [FieldOffset(76)] public uint OreHash;
        [FieldOffset(80)] public uint Quantity;
        [FieldOffset(84)] public uint Frame;
        [FieldOffset(88)] public float VfxEmissionMultiplier;
        [FieldOffset(92)] public byte SourceKind;
        [FieldOffset(93)] public byte Flags;
        [FieldOffset(94)] public ushort _pad0;
        [FieldOffset(96)] public ulong _pad1;
        [FieldOffset(104)] public ulong _pad2;
        [FieldOffset(112)] public ulong _pad3;
        [FieldOffset(120)] public ulong _pad4;
    }
}
