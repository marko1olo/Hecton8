using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.World
{
    internal static class HLODInstanceLayout
    {
        public const int HLODInstanceStrideBytes = 96;
    }

    /// <summary>
    /// Cartographer-published far-field HLOD instance payload.
    /// Coordinates stay in local runtime space and are shifted on GPU.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = HLODInstanceLayout.HLODInstanceStrideBytes)]
    public struct HLODInstance
    {
        [FieldOffset(0)]
        public Matrix4x4 LocalToWorld;
        [FieldOffset(64)]
        public Bounds LocalBounds;
        [FieldOffset(88)]
        public float Fade01;
        [FieldOffset(92)]
        private uint _pad0;
    }
}
