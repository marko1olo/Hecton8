using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Blittable brine plane sample for allocation-free handoff between world sampling and runtime consumers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct BrineLayerSample
    {
        public int2 CartographySector;
        public float AbsoluteHeightY;
        public float RuntimeHeightY;
        public float DensityMultiplier;
        public float Toxicity01;
        public byte Flags;
        public byte Reserved0;
        public ushort SectorHash;
        private uint _pad0;
    }
}
