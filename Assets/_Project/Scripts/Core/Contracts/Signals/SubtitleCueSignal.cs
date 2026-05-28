using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Hash-addressed VWS/Babel subtitle cue signal. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubtitleCueSignal : ISignal
    {
        public const int ExpectedCapacity = 32;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 64;
        public const uint LaneHash = 0x53554331u; // SUC1

        [FieldOffset(0)] public uint TokenHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint StartAudioFrame;
        [FieldOffset(12)] public uint AudioFrameLatency;
        [FieldOffset(16)] public ushort DurationMilliseconds;
        [FieldOffset(18)] public byte Priority;
        [FieldOffset(19)] public byte Flags;
        [FieldOffset(20)] private byte _pad0;
        [FieldOffset(21)] private byte _pad1;
        [FieldOffset(22)] private byte _pad2;
        [FieldOffset(23)] private byte _pad3;
        [FieldOffset(24)] private byte _pad4;
        [FieldOffset(25)] private byte _pad5;
        [FieldOffset(26)] private byte _pad6;
        [FieldOffset(27)] private byte _pad7;
        [FieldOffset(28)] private byte _pad8;
        [FieldOffset(29)] private byte _pad9;
        [FieldOffset(30)] private byte _pad10;
        [FieldOffset(31)] private byte _pad11;
        [FieldOffset(32)] private byte _pad12;
        [FieldOffset(33)] private byte _pad13;
        [FieldOffset(34)] private byte _pad14;
        [FieldOffset(35)] private byte _pad15;
        [FieldOffset(36)] private byte _pad16;
        [FieldOffset(37)] private byte _pad17;
        [FieldOffset(38)] private byte _pad18;
        [FieldOffset(39)] private byte _pad19;
        [FieldOffset(40)] private byte _pad20;
        [FieldOffset(41)] private byte _pad21;
        [FieldOffset(42)] private byte _pad22;
        [FieldOffset(43)] private byte _pad23;
        [FieldOffset(44)] private byte _pad24;
        [FieldOffset(45)] private byte _pad25;
        [FieldOffset(46)] private byte _pad26;
        [FieldOffset(47)] private byte _pad27;
        [FieldOffset(48)] private byte _pad28;
        [FieldOffset(49)] private byte _pad29;
        [FieldOffset(50)] private byte _pad30;
        [FieldOffset(51)] private byte _pad31;
        [FieldOffset(52)] private byte _pad32;
        [FieldOffset(53)] private byte _pad33;
        [FieldOffset(54)] private byte _pad34;
        [FieldOffset(55)] private byte _pad35;
        [FieldOffset(56)] private byte _pad36;
        [FieldOffset(57)] private byte _pad37;
        [FieldOffset(58)] private byte _pad38;
        [FieldOffset(59)] private byte _pad39;
        [FieldOffset(60)] private byte _pad40;
        [FieldOffset(61)] private byte _pad41;
        [FieldOffset(62)] private byte _pad42;
        [FieldOffset(63)] private byte _pad43;
    }
}
