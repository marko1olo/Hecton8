using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Input.Universal
{
    /// <summary>
    /// Hardware-agnostic deterministic input payload shape for cross-assembly contracts.
    /// Runtime publication uses the core determinism bridge queue.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct UniversalInputStateSignal
    {
        public float2 Move;
        public float2 Look;
        public float Vertical;
        public uint ActionsBitmask;
        public uint CurrentInputSchemeHash;
        public uint Frame;
        public uint Sequence;
        public byte Flags;
        private byte _pad0;
        private byte _pad1;
        private byte _pad2;
        private byte _pad3;
        private byte _pad4;
        private byte _pad5;
        private byte _pad6;
        private byte _pad7;
        private byte _pad8;
        private byte _pad9;
        private byte _pad10;
    }
}
