using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HectonAabb
    {
        [FieldOffset(0)]
        public float3 Min;

        [FieldOffset(12)]
        public float3 Max;

        [FieldOffset(24)]
        public float2 Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HectonSphere
    {
        [FieldOffset(0)]
        public float3 Center;

        [FieldOffset(12)]
        public float Radius;
    }

    /// <summary>
    /// Burst vector spatial predicates for four-lane culling and simulation checks.
    /// </summary>
    public static class HectonSpatialIntrinsics
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this HectonAabb aabb, float3 point)
        {
            return point.x >= aabb.Min.x &&
                   point.y >= aabb.Min.y &&
                   point.z >= aabb.Min.z &&
                   point.x <= aabb.Max.x &&
                   point.y <= aabb.Max.y &&
                   point.z <= aabb.Max.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Intersects(this HectonSphere sphere, HectonSphere other)
        {
            float3 delta = other.Center - sphere.Center;
            float radius = sphere.Radius + other.Radius;
            return math.lengthsq(delta) <= radius * radius;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ContainsMask4(
            this HectonAabb aabb,
            float4 pointX,
            float4 pointY,
            float4 pointZ)
        {
            if (X86.Sse.IsSseSupported)
            {
                return aabb.ContainsMask4(
                    new v128(pointX.x, pointX.y, pointX.z, pointX.w),
                    new v128(pointY.x, pointY.y, pointY.z, pointY.w),
                    new v128(pointZ.x, pointZ.y, pointZ.z, pointZ.w));
            }

            bool4 inside =
                pointX >= aabb.Min.x &
                pointY >= aabb.Min.y &
                pointZ >= aabb.Min.z &
                pointX <= aabb.Max.x &
                pointY <= aabb.Max.y &
                pointZ <= aabb.Max.z;

            return math.bitmask(inside) & 0xF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ContainsMask4(
            this HectonAabb aabb,
            v128 pointX,
            v128 pointY,
            v128 pointZ)
        {
            if (!X86.Sse.IsSseSupported)
                return ContainsMask4Fallback(in aabb, pointX, pointY, pointZ);

            v128 minX = X86.Sse.set1_ps(aabb.Min.x);
            v128 minY = X86.Sse.set1_ps(aabb.Min.y);
            v128 minZ = X86.Sse.set1_ps(aabb.Min.z);
            v128 maxX = X86.Sse.set1_ps(aabb.Max.x);
            v128 maxY = X86.Sse.set1_ps(aabb.Max.y);
            v128 maxZ = X86.Sse.set1_ps(aabb.Max.z);

            v128 mask = X86.Sse.and_ps(
                X86.Sse.and_ps(
                    X86.Sse.and_ps(CmpGe(pointX, minX), CmpGe(pointY, minY)),
                    X86.Sse.and_ps(CmpGe(pointZ, minZ), CmpLe(pointX, maxX))),
                X86.Sse.and_ps(CmpLe(pointY, maxY), CmpLe(pointZ, maxZ)));

            return X86.Sse.movemask_ps(mask) & 0xF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IntersectsAabbMask4(
            this HectonAabb query,
            v128 minX,
            v128 minY,
            v128 minZ,
            v128 maxX,
            v128 maxY,
            v128 maxZ)
        {
            if (!X86.Sse.IsSseSupported)
                return IntersectsAabbMask4Fallback(in query, minX, minY, minZ, maxX, maxY, maxZ);

            v128 queryMinX = X86.Sse.set1_ps(query.Min.x);
            v128 queryMinY = X86.Sse.set1_ps(query.Min.y);
            v128 queryMinZ = X86.Sse.set1_ps(query.Min.z);
            v128 queryMaxX = X86.Sse.set1_ps(query.Max.x);
            v128 queryMaxY = X86.Sse.set1_ps(query.Max.y);
            v128 queryMaxZ = X86.Sse.set1_ps(query.Max.z);

            v128 mask = X86.Sse.and_ps(
                X86.Sse.and_ps(
                    X86.Sse.and_ps(CmpLe(minX, queryMaxX), CmpLe(minY, queryMaxY)),
                    X86.Sse.and_ps(CmpLe(minZ, queryMaxZ), CmpGe(maxX, queryMinX))),
                X86.Sse.and_ps(CmpGe(maxY, queryMinY), CmpGe(maxZ, queryMinZ)));

            return X86.Sse.movemask_ps(mask) & 0xF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IntersectsMask4(
            this HectonSphere sphere,
            float4 centerX,
            float4 centerY,
            float4 centerZ,
            float4 radius)
        {
            if (X86.Sse.IsSseSupported)
            {
                return sphere.IntersectsMask4(
                    new v128(centerX.x, centerX.y, centerX.z, centerX.w),
                    new v128(centerY.x, centerY.y, centerY.z, centerY.w),
                    new v128(centerZ.x, centerZ.y, centerZ.z, centerZ.w),
                    new v128(radius.x, radius.y, radius.z, radius.w));
            }

            float4 dx = centerX - sphere.Center.x;
            float4 dy = centerY - sphere.Center.y;
            float4 dz = centerZ - sphere.Center.z;
            float4 combined = radius + sphere.Radius;
            float4 distSq = (dx * dx) + (dy * dy) + (dz * dz);

            return math.bitmask(distSq <= (combined * combined)) & 0xF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IntersectsMask4(
            this HectonSphere sphere,
            v128 centerX,
            v128 centerY,
            v128 centerZ,
            v128 radius)
        {
            if (!X86.Sse.IsSseSupported)
                return IntersectsMask4Fallback(in sphere, centerX, centerY, centerZ, radius);

            v128 sphereX = X86.Sse.set1_ps(sphere.Center.x);
            v128 sphereY = X86.Sse.set1_ps(sphere.Center.y);
            v128 sphereZ = X86.Sse.set1_ps(sphere.Center.z);
            v128 sphereRadius = X86.Sse.set1_ps(sphere.Radius);

            v128 dx = X86.Sse.sub_ps(centerX, sphereX);
            v128 dy = X86.Sse.sub_ps(centerY, sphereY);
            v128 dz = X86.Sse.sub_ps(centerZ, sphereZ);
            v128 combined = X86.Sse.add_ps(radius, sphereRadius);
            v128 distSq = X86.Sse.add_ps(
                X86.Sse.add_ps(X86.Sse.mul_ps(dx, dx), X86.Sse.mul_ps(dy, dy)),
                X86.Sse.mul_ps(dz, dz));
            v128 radiusSq = X86.Sse.mul_ps(combined, combined);

            return X86.Sse.movemask_ps(CmpLe(distSq, radiusSq)) & 0xF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ContainsMask4Fallback(in HectonAabb aabb, v128 pointX, v128 pointY, v128 pointZ)
        {
            int mask = 0;
            mask |= ContainsLane(in aabb, pointX.Float0, pointY.Float0, pointZ.Float0) ? 1 : 0;
            mask |= ContainsLane(in aabb, pointX.Float1, pointY.Float1, pointZ.Float1) ? 2 : 0;
            mask |= ContainsLane(in aabb, pointX.Float2, pointY.Float2, pointZ.Float2) ? 4 : 0;
            mask |= ContainsLane(in aabb, pointX.Float3, pointY.Float3, pointZ.Float3) ? 8 : 0;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IntersectsMask4Fallback(in HectonSphere sphere, v128 centerX, v128 centerY, v128 centerZ, v128 radius)
        {
            int mask = 0;
            mask |= IntersectsLane(in sphere, centerX.Float0, centerY.Float0, centerZ.Float0, radius.Float0) ? 1 : 0;
            mask |= IntersectsLane(in sphere, centerX.Float1, centerY.Float1, centerZ.Float1, radius.Float1) ? 2 : 0;
            mask |= IntersectsLane(in sphere, centerX.Float2, centerY.Float2, centerZ.Float2, radius.Float2) ? 4 : 0;
            mask |= IntersectsLane(in sphere, centerX.Float3, centerY.Float3, centerZ.Float3, radius.Float3) ? 8 : 0;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IntersectsAabbMask4Fallback(
            in HectonAabb query,
            v128 minX,
            v128 minY,
            v128 minZ,
            v128 maxX,
            v128 maxY,
            v128 maxZ)
        {
            int mask = 0;
            mask |= IntersectsAabbLane(in query, minX.Float0, minY.Float0, minZ.Float0, maxX.Float0, maxY.Float0, maxZ.Float0) ? 1 : 0;
            mask |= IntersectsAabbLane(in query, minX.Float1, minY.Float1, minZ.Float1, maxX.Float1, maxY.Float1, maxZ.Float1) ? 2 : 0;
            mask |= IntersectsAabbLane(in query, minX.Float2, minY.Float2, minZ.Float2, maxX.Float2, maxY.Float2, maxZ.Float2) ? 4 : 0;
            mask |= IntersectsAabbLane(in query, minX.Float3, minY.Float3, minZ.Float3, maxX.Float3, maxY.Float3, maxZ.Float3) ? 8 : 0;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsLane(in HectonAabb aabb, float x, float y, float z)
        {
            return x >= aabb.Min.x &&
                   y >= aabb.Min.y &&
                   z >= aabb.Min.z &&
                   x <= aabb.Max.x &&
                   y <= aabb.Max.y &&
                   z <= aabb.Max.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IntersectsLane(in HectonSphere sphere, float x, float y, float z, float radius)
        {
            float dx = x - sphere.Center.x;
            float dy = y - sphere.Center.y;
            float dz = z - sphere.Center.z;
            float combined = radius + sphere.Radius;
            return (dx * dx) + (dy * dy) + (dz * dz) <= combined * combined;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IntersectsAabbLane(in HectonAabb query, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            return minX <= query.Max.x &&
                   minY <= query.Max.y &&
                   minZ <= query.Max.z &&
                   maxX >= query.Min.x &&
                   maxY >= query.Min.y &&
                   maxZ >= query.Min.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static v128 CmpLe(v128 left, v128 right)
        {
            return X86.Avx.IsAvxSupported
                ? X86.Avx.cmp_ps(left, right, (int)X86.Avx.CMP.LE_OQ)
                : X86.Sse.cmple_ps(left, right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static v128 CmpGe(v128 left, v128 right)
        {
            return X86.Avx.IsAvxSupported
                ? X86.Avx.cmp_ps(left, right, (int)X86.Avx.CMP.GE_OQ)
                : X86.Sse.cmpge_ps(left, right);
        }
    }
}
