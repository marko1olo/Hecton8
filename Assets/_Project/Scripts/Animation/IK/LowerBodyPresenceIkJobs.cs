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
        public const int FootIkDataBytes = 68;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<FootIKData>() == FootIkDataBytes;
        }
    }

    /// <summary>
    /// Packed per-foot state for Burst lower-body presence.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FootIKData
    {
        public float3 TargetPosition;
        public float3 CurrentPosition;
        public float3 StepStartPosition;
        public float3 SurfaceNormal;
        public float StepProgress01;
        public float StepThresholdSq;
        public float StepHeightMeters;
        public float Blend;
        public byte Flags;
        public byte Side;
        public ushort Reserved;
    }
}
