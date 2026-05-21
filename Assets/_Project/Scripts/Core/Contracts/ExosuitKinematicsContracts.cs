using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class ExosuitInputActions
    {
        public const uint Grab = 1u << 0;
        public const uint Purge = 1u << 1;
        public const uint Jump = 1u << 2;
        public const uint ExternalAuthority = 1u << 31;
    }

    /// <summary>
    /// 32-byte unmanaged exosuit frame intent written by player/input authority and consumed by the kinematic owner.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ExosuitFrameInputDTO
    {
        [FieldOffset(0)] public float2 MoveAxis;
        [FieldOffset(8)] public float VerticalAxis;
        [FieldOffset(12)] public float DesiredYawRadians;
        [FieldOffset(16)] public uint ActionMask;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] private uint _pad0;
    }
}
