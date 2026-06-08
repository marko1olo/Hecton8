using System.Runtime.InteropServices;

namespace Hecton8.Data
{
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct H8AppliedLoreWorldImpactRecord
    {
        public const int SizeBytes = 24;

        [FieldOffset(0)]
        public uint PacketHash;

        [FieldOffset(4)]
        public uint BiomeHash;

        [FieldOffset(8)]
        public float AcousticIntensity01;

        [FieldOffset(12)]
        public float AcousticPitchScale;

        [FieldOffset(16)]
        public byte Flags;

        [FieldOffset(17)]
        private byte _pad0;

        [FieldOffset(18)]
        private ushort _pad1;

        [FieldOffset(20)]
        private uint _pad2;
    }
}
