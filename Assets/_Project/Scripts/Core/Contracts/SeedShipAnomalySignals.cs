using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class SeedShipAnomalySignalLayout
    {
        public const int GlitchCommandStrideBytes = 16;
        public const int MockAupRebaseSignalStrideBytes = 32;
        public const int RadarJamSignalStrideBytes = 32;
        public const int CoreHackedSignalStrideBytes = 32;
        public const int MockHudSignalStrideBytes = 32;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeedShipAnomalySignalLayout.GlitchCommandStrideBytes)]
    public struct GlitchCommandDTO
    {
        [FieldOffset(0)] public float Intensity;
        [FieldOffset(4)] public float Frequency;
        [FieldOffset(8)] public uint GlyphHash;
        [FieldOffset(12)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeedShipAnomalySignalLayout.MockAupRebaseSignalStrideBytes)]
    public partial struct MockAupRebaseSignal : ISignal
    {
        [FieldOffset(0)] public float3 ShiftMeters;
        [FieldOffset(12)] public uint ShiftFrameId;
        [FieldOffset(16)] public int3 SectorDelta;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeedShipAnomalySignalLayout.RadarJamSignalStrideBytes)]
    public partial struct RadarJamSignal : ISignal
    {
        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float Frequency;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public float Phase01;
        [FieldOffset(20)] public float DropLock01;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] public byte Reserved0;
        [FieldOffset(26)] public ushort Reserved1;
        [FieldOffset(28)] public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeedShipAnomalySignalLayout.CoreHackedSignalStrideBytes)]
    public partial struct CoreHackedSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint CodeHash;
        [FieldOffset(12)] public float Validity01;
        [FieldOffset(16)] public byte Flags;
        [FieldOffset(17)] public byte Reserved0;
        [FieldOffset(18)] public ushort Reserved1;
        [FieldOffset(20)] public uint Reserved2;
        [FieldOffset(24)] public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = SeedShipAnomalySignalLayout.MockHudSignalStrideBytes)]
    public partial struct MockHudSignal : ISignal
    {
        [FieldOffset(0)] public GlitchCommandDTO Command;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public float Corruption01;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte Reserved0;
        [FieldOffset(30)] public ushort Reserved1;
    }
}
