using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Input.Universal
{
    /// <summary>
    /// Hardware-agnostic deterministic input payload shape for cross-assembly contracts.
    /// Runtime publication uses the core determinism bridge queue.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct UniversalInputStateSignal
    {
        [FieldOffset(0)] public float2 Move;
        [FieldOffset(8)] public float2 Look;
        [FieldOffset(16)] public float Vertical;
        [FieldOffset(20)] public uint ActionsBitmask;
        [FieldOffset(24)] public uint CurrentInputSchemeHash;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public uint Sequence;
        [FieldOffset(36)] public byte Flags;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
        [FieldOffset(40)] private byte _pad3;
        [FieldOffset(41)] private byte _pad4;
        [FieldOffset(42)] private byte _pad5;
        [FieldOffset(43)] private byte _pad6;
        [FieldOffset(44)] private byte _pad7;
        [FieldOffset(45)] private byte _pad8;
        [FieldOffset(46)] private byte _pad9;
        [FieldOffset(47)] private byte _pad10;
    }
}
