using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Input.Universal
{
    /// <summary>
    /// Hardware-agnostic deterministic input payload shape for cross-assembly contracts.
    /// Runtime publication uses the core determinism bridge queue.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
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
    }
}
