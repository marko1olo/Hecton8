using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Animation.IK
{
    /// <summary>
    /// Fixed lower-body IK lane constants shared by runtime jobs and animation consumers.
    /// </summary>
    public static class LowerBodyPresenceIkConstants
    {
        /// <summary>Number of foot lanes per contextual IK entity.</summary>
        public const int FeetPerEntity = 2;

        /// <summary>SOA index for the left foot lane.</summary>
        public const int LeftFootIndex = 0;

        /// <summary>SOA index for the right foot lane.</summary>
        public const int RightFootIndex = 1;

        /// <summary>Foot has an accepted seabed contact target.</summary>
        public const byte FlagGrounded = 1 << 0;

        /// <summary>Foot is in a visual step arc.</summary>
        public const byte FlagStepping = 1 << 1;

        /// <summary>Foot is using the swimming posture fallback.</summary>
        public const byte FlagSwimming = 1 << 2;

        /// <summary>Foot data was sanitized from a non-finite input.</summary>
        public const byte FlagInvalid = 1 << 7;
    }

    /// <summary>
    /// ABI sentinel for lower-body IK payloads shared through native lanes.
    /// </summary>
    public static class LowerBodyPresenceIkLayout
    {
        public const int FootIkDataBytes = 128;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<FootIKData>() == FootIkDataBytes;
        }
    }

    /// <summary>
    /// Packed per-foot state for Burst lower-body presence.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct FootIKData
    {
        [FieldOffset(0)] public float3 TargetPosition;
        [FieldOffset(12)] public float3 CurrentPosition;
        [FieldOffset(24)] public float3 StepStartPosition;
        [FieldOffset(36)] public float3 SurfaceNormal;
        [FieldOffset(48)] public float StepProgress01;
        [FieldOffset(52)] public float StepThresholdSq;
        [FieldOffset(56)] public float StepHeightMeters;
        [FieldOffset(60)] public float Blend;
        [FieldOffset(64)] public byte Flags;
        [FieldOffset(65)] public byte Side;
        [FieldOffset(66)] public ushort Reserved;
        [FieldOffset(68)] private uint _pad0;
        [FieldOffset(72)] private ulong _pad1;
        [FieldOffset(80)] private ulong _pad2;
        [FieldOffset(88)] private ulong _pad3;
        [FieldOffset(96)] private ulong _pad4;
        [FieldOffset(104)] private ulong _pad5;
        [FieldOffset(112)] private ulong _pad6;
        [FieldOffset(120)] private ulong _pad7;
    }
}
