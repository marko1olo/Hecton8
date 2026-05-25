using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Audio.Echolocation
{
    /// <summary>
    /// Blittable return payload for one virtual active-sonar reflection tap.
    /// Contract-owned so Core/audio consumers do not reference the echolocation runtime assembly.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct AcousticEcholocationRayHit
    {
        [FieldOffset(0)]
        public float3 Point;
        [FieldOffset(12)]
        public float3 Direction;
        [FieldOffset(24)]
        public float RayDistanceMeters;
        [FieldOffset(28)]
        public float ReturnDistanceMeters;
        [FieldOffset(32)]
        public float DelaySeconds;
        [FieldOffset(36)]
        public float Gain;
        [FieldOffset(40)]
        public float LowPassCutoffHertz;
        [FieldOffset(44)]
        public byte AudioMaterialId;
        [FieldOffset(45)]
        public byte Hit;
        [FieldOffset(46)]
        public ushort Reserved;
        [FieldOffset(48)]
        public uint StateHash;
        [FieldOffset(52)]
        private uint _reserved1;
    }
}
