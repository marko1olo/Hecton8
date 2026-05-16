using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Acoustic-space absolute universe position used by audio propagation and voice virtualization.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticAup
    {
        public const int CellSizeMeters = 5000;

        public long GridX;
        public long GridY;
        public long GridZ;
        public float3 Local;

        public AcousticAup(long gridX, long gridY, long gridZ, float3 local)
        {
            GridX = gridX;
            GridY = gridY;
            GridZ = gridZ;
            Local = local;
        }

        public static float3 RelativeFloat3(in AcousticAup position, in AcousticAup origin)
        {
            const double maxFloatSafe = 1000000000000.0;
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

            return (float)math.min(1000000000.0, math.sqrt(distanceSq));
        }

        public static bool IsFinite(in AcousticAup aup)
        {
            return math.all(math.isfinite(aup.Local));
        }
    }
}
