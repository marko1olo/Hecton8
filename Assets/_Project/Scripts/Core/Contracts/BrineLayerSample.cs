using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Blittable brine plane sample for allocation-free handoff between world sampling and runtime consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BrineLayerSample
    {
        [FieldOffset(0)] public int2 CartographySector;
        [FieldOffset(8)] public float AbsoluteHeightY;
        [FieldOffset(12)] public float RuntimeHeightY;
        [FieldOffset(16)] public float DensityMultiplier;
        [FieldOffset(20)] public float Toxicity01;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] public byte Reserved0;
        [FieldOffset(26)] public ushort SectorHash;
        [FieldOffset(28)] private uint _pad0;
    }
}
