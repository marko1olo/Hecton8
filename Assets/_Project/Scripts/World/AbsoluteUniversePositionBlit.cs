using System.Runtime.InteropServices;
using Hecton8.Core.Memory.Layout;
using Unity.Mathematics;

namespace Hecton8.World
{
    internal static class AbsoluteUniversePositionBlitLayout
    {
        public const int AbsoluteUniversePositionBlitStrideBytes = 48;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = AbsoluteUniversePositionBlitLayout.AbsoluteUniversePositionBlitStrideBytes)]
    public struct AbsoluteUniversePositionBlit
    {
        [FieldOffset(0)]
        public long GridX;

        [FieldOffset(8)]
        public long GridY;

        [FieldOffset(16)]
        public long GridZ;

        [FieldOffset(24)]
        public float3 Local;

        [FieldOffset(36)]
        public uint Reserved0;

        [FieldOffset(40)]
        public ulong Reserved1;

        public static AbsoluteUniversePositionBlit FromAup(in AbsoluteUniversePosition position)
        {
            return new AbsoluteUniversePositionBlit
            {
                GridX = position.GridX,
                GridY = position.GridY,
                GridZ = position.GridZ,
                Local = new float3(position.LocalX, position.LocalY, position.LocalZ),
                Reserved0 = 0u,
                Reserved1 = 0UL
            };
        }

        public AbsoluteUniversePosition ToAup()
        {
            return new AbsoluteUniversePosition
            {
                GridX = GridX,
                GridY = GridY,
                GridZ = GridZ,
                LocalX = Local.x,
                LocalY = Local.y,
                LocalZ = Local.z
            };
        }
    }
}
