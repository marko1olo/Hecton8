using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Acoustic-space absolute universe position used by audio propagation and voice virtualization.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct AcousticAup
    {
        public const int CellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;

        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float3 Local;
        [FieldOffset(36)] private uint _pad0;

        public AcousticAup(long gridX, long gridY, long gridZ, float3 local)
        {
            GridX = gridX;
            GridY = gridY;
            GridZ = gridZ;
            Local = local;
            _pad0 = 0u;
        }

        public static float3 RelativeFloat3(in AcousticAup position, in AcousticAup origin)
        {
            const double maxFloatSafe = HectonPhysicsContract.AupMaxFloatSafeMeters;
            double cellSize = CellSizeMeters;
            double x = ((position.GridX - origin.GridX) * cellSize) + (double)position.Local.x - origin.Local.x;
            double y = ((position.GridY - origin.GridY) * cellSize) + (double)position.Local.y - origin.Local.y;
            double z = ((position.GridZ - origin.GridZ) * cellSize) + (double)position.Local.z - origin.Local.z;
            return new float3(
                (float)math.clamp(x, -maxFloatSafe, maxFloatSafe),
                (float)math.clamp(y, -maxFloatSafe, maxFloatSafe),
                (float)math.clamp(z, -maxFloatSafe, maxFloatSafe));
        }

        public static float DistanceMeters(in AcousticAup a, in AcousticAup b)
        {
            double cellSize = CellSizeMeters;
            double x = ((a.GridX - b.GridX) * cellSize) + (double)a.Local.x - b.Local.x;
            double y = ((a.GridY - b.GridY) * cellSize) + (double)a.Local.y - b.Local.y;
            double z = ((a.GridZ - b.GridZ) * cellSize) + (double)a.Local.z - b.Local.z;
            double distanceSq = x * x + y * y + z * z;
            if (distanceSq <= 0.0 || !math.isfinite(distanceSq))
                return 0f;

            return (float)math.min(HectonPhysicsContract.AupMaxDistanceReturnMeters, math.sqrt(distanceSq));
        }

        public static bool IsFinite(in AcousticAup aup)
        {
            return math.all(math.isfinite(aup.Local));
        }
    }
}
