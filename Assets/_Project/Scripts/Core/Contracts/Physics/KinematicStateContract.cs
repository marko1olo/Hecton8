using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Physics
{
    internal static class KinematicStateLayout
    {
        internal const int KinematicStateStrideBytes = 64;
    }

    [StructLayout(LayoutKind.Explicit, Size = KinematicStateLayout.KinematicStateStrideBytes)]
    public struct KinematicStateDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float3 AngularVelocity;
        [FieldOffset(48)] public float Mass;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float DragCoefficient;
        [FieldOffset(60)] public byte RestingFrameCount;
        [FieldOffset(61)] public byte DeepSleepTickCount;
        [FieldOffset(62)] public byte SleepMaterialIndex;
        [FieldOffset(63)] public byte _pad0;
    }
}
