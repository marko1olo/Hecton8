using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.Environment.Fluids
{
    /// <summary>
    /// Burst-safe scalar brine-plane math. No object references, no allocations.
    /// </summary>
    [BurstCompile]
    public static class BrineLayerMath
    {
        public static int2 ResolveCartographySector(float3 runtimePosition, float3 shiftOffset)
        {
            float3 absolute = runtimePosition + shiftOffset;
            float invSectorSize = math.rcp(BrineLayerConstants.CartographySectorSizeMeters);
            return new int2(
                (int)math.floor(absolute.x * invSectorSize),
                (int)math.floor(absolute.z * invSectorSize));
        }

        public static float ResolveRuntimeHeightY(float absoluteHeightY, float shiftOffsetY)
        {
            return math.isfinite(absoluteHeightY) && math.isfinite(shiftOffsetY)
                ? absoluteHeightY - shiftOffsetY
                : float.NegativeInfinity;
        }

        public static bool IsRuntimeBelowAbsolutePlane(float runtimeY, float absoluteHeightY, float shiftOffsetY)
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
