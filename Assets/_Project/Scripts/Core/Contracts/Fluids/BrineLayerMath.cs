using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Fluids
{
    /// <summary>
    /// Burst-safe scalar brine-plane math. No object references, no allocations.
    /// </summary>
    public static class BrineLayerMath
    {
        public static int2 ResolveCartographySector(float3 runtimePosition, float3 shiftOffset)
        {
            return ResolveCartographySector(
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z));
        }

        public static int2 ResolveCartographySector(float3 runtimePosition, double3 shiftOffset)
        {
            return ResolveCartographySector(
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                shiftOffset);
        }

        private static int2 ResolveCartographySector(double3 runtimePosition, double3 shiftOffset)
        {
            double3 absolute = runtimePosition + shiftOffset;
            double invSectorSize = math.rcp((double)BrineLayerConstants.CartographySectorSizeMeters);
            return new int2(
                (int)math.floor(absolute.x * invSectorSize),
                (int)math.floor(absolute.z * invSectorSize));
        }

        public static float ResolveRuntimeHeightY(float absoluteHeightY, float shiftOffsetY)
        {
            return ResolveRuntimeHeightY(absoluteHeightY, (double)shiftOffsetY);
        }

        public static float ResolveRuntimeHeightY(float absoluteHeightY, double shiftOffsetY)
        {
            return math.isfinite(absoluteHeightY) && math.isfinite(shiftOffsetY)
                ? (float)(absoluteHeightY - shiftOffsetY)
                : float.NegativeInfinity;
        }

        public static bool IsRuntimeBelowAbsolutePlane(float runtimeY, float absoluteHeightY, float shiftOffsetY)
        {
            return IsRuntimeBelowAbsolutePlane(runtimeY, absoluteHeightY, (double)shiftOffsetY);
        }

        public static bool IsRuntimeBelowAbsolutePlane(float runtimeY, float absoluteHeightY, double shiftOffsetY)
        {
            return math.isfinite(runtimeY) &&
                   runtimeY < ResolveRuntimeHeightY(absoluteHeightY, shiftOffsetY);
        }

        public static ushort ResolveSectorHash(int2 sector)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)sector.x) * 16777619u;
            hash = (hash ^ (uint)sector.y) * 16777619u;
            return (ushort)(hash ^ (hash >> 16));
        }
    }
}
